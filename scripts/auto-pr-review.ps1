# Ensure Ollama has a model loaded
$ollamaRunning = $false
try {
    $ps = Invoke-RestMethod -Uri "http://localhost:11434/api/ps" -ErrorAction Stop
    if ($ps.models.Count -gt 0) { $ollamaRunning = $true }
} catch {}

if (-not $ollamaRunning) {
    Write-Host "Loading qwen3:14b..."
    Start-Process -FilePath "ollama" -ArgumentList "run qwen3:14b" -WindowStyle Hidden
    Start-Sleep -Seconds 30
}

$webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }
$reviewedFile = "$HOME\.office-rag-db\reviewed-prs.json"

if (Test-Path $reviewedFile) {
    $reviewed = Get-Content $reviewedFile | ConvertFrom-Json
} else {
    $reviewed = @()
}

$repos = @("Koraji95-coder/Office", "Koraji95-coder/Suite")

foreach ($repo in $repos) {
    try {
        $prs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls?state=open&per_page=10" -Headers $headers

        foreach ($pr in $prs) {
            if ($pr.user.login -ne "copilot-swe-agent[bot]" -and $pr.user.login -ne "Copilot") { continue }

            $prKey = "$repo#$($pr.number)@$($pr.updated_at)"
            if ($reviewed -contains $prKey) { continue }

            $diffHeaders = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3.diff" }
            $diff = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $diffHeaders

            if ($diff.Length -gt 6000) {
                $diff = $diff.Substring(0, 6000) + "`n... (truncated)"
            }

            $ragContext = ""
            try {
                $ragOutput = & python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $pr.title 2>$null
                $ragContext = $ragOutput | Out-String
                if ($ragContext.Length -gt 2000) {
                    $ragContext = $ragContext.Substring(0, 2000)
                }
            } catch {}

            $reviewPrompt = @"
You are a senior code reviewer. Review this pull request and provide a brief assessment.

PR Title: $($pr.title)
PR Description: $($pr.body)

DIFF:
$diff

RELATED CODEBASE CONTEXT:
$ragContext

Provide a review with:
1. **Verdict**: APPROVE, REQUEST_CHANGES, or NEEDS_DISCUSSION
2. **Summary**: 2-3 sentences on what this PR does
3. **Concerns**: Any issues found (or "None")
4. **Quality**: Rate 1-10

Keep it concise. No fluff.
"@

            $chatBody = @{
                model    = "qwen3:14b"
                messages = @(@{ role = "user"; content = $reviewPrompt })
                stream   = $false
            } | ConvertTo-Json -Depth 3

            $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
            $review = $response.message.content

            $color = 8421504
            if ($review -match "APPROVE") { $color = 5793266 }
            if ($review -match "REQUEST_CHANGES") { $color = 16744256 }
            if ($review -match "NEEDS_DISCUSSION") { $color = 16776960 }

            $repoShort = $repo.Split("/")[1]

            # Post Discord notification
            $payload = @{
                content = "<@$userId>"
                embeds = @(@{
                    title       = "PR Review: $($pr.title)"
                    description = "$review`n`n[View PR]($($pr.html_url))"
                    color       = $color
                    footer      = @{ text = "$repoShort | PR #$($pr.number) | Auto-Review" }
                    timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                })
            }
            $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
            Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))

            # Submit actual GitHub PR review (APPROVE/REQUEST_CHANGES), not just a comment
            $ghReviewEvent = "COMMENT"
            if ($review -match "APPROVE" -and $review -notmatch "REQUEST_CHANGES") {
                $ghReviewEvent = "APPROVE"
            } elseif ($review -match "REQUEST_CHANGES") {
                $ghReviewEvent = "REQUEST_CHANGES"
            }

            try {
                $reviewBody = @{
                    body  = "## Auto-Review (Ollama)`n`n$review`n`n---`n*Automated review powered by qwen3:14b + RAG context*"
                    event = $ghReviewEvent
                } | ConvertTo-Json -Compress
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" `
                    -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" `
                    -Body ([System.Text.Encoding]::UTF8.GetBytes($reviewBody))
                Write-Host "Submitted $ghReviewEvent review on $repo#$($pr.number)"
            } catch {
                Write-Host "Failed to submit review on $repo#$($pr.number): $($_.Exception.Message)"
                # Fallback to comment
                try {
                    $commentBody = @{
                        body = "## Auto-Review (Ollama)`n`n$review`n`n---`n*Automated review powered by qwen3:14b + RAG context*"
                    } | ConvertTo-Json -Compress
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($pr.number)/comments" `
                        -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" `
                        -Body ([System.Text.Encoding]::UTF8.GetBytes($commentBody))
                } catch {}
            }

            # Auto-merge if APPROVE and quality >= 9
            $shouldMerge = $false
            if ($review -match "APPROVE" -and $review -notmatch "REQUEST_CHANGES" -and $review -notmatch "NEEDS_DISCUSSION") {
                if ($review -match "(\d+)\s*/\s*10") {
                    $score = [int]$Matches[1]
                    if ($score -ge 9) { $shouldMerge = $true }
                }
            }

            if ($shouldMerge) {
                try {
                    # Step 1: Mark draft PR as ready for review
                    if ($pr.draft -eq $true) {
                        Write-Host "Marking $repo#$($pr.number) as ready for review..."
                        $readyBody = @{
                            query = "mutation { markPullRequestReadyForReview(input: { pullRequestId: `"$($pr.node_id)`" }) { pullRequest { isDraft } } }"
                        } | ConvertTo-Json -Compress
                        $readyResult = Invoke-RestMethod -Uri "https://api.github.com/graphql" -Method POST -Headers @{
                            Authorization = "Bearer $ghToken"
                        } -ContentType "application/json" -Body $readyBody

                        if ($readyResult.data.markPullRequestReadyForReview.pullRequest.isDraft -eq $true) {
                            Write-Host "Failed to mark $repo#$($pr.number) as ready — skipping merge."
                            $reviewed += $prKey
                            continue
                        }
                        Write-Host "Marked $repo#$($pr.number) as ready."
                        Start-Sleep -Seconds 3
                    }

                    # Step 2: Merge
                    $mergeBody = @{
                        commit_title = "auto-merge: $($pr.title) [score $score/10]"
                        merge_method = "squash"
                    } | ConvertTo-Json -Compress
                    try {
                        Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/merge" `
                            -Method PUT -Headers $headers -ContentType "application/json" -Body $mergeBody
                        Write-Host "AUTO-MERGED: $repo#$($pr.number) — score $score/10"
                    } catch {
                        if ($_.Exception.Response.StatusCode -eq 405 -or $_.Exception.Response.StatusCode -eq 409) {
                            Write-Host "Merge blocked on $repo#$($pr.number) — attempting branch update..."
                            try {
                                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/update-branch" `
                                    -Method PUT -Headers $headers -ContentType "application/json" `
                                    -Body ('{"expected_head_sha":"' + $pr.head.sha + '"}')
                                Write-Host "Branch updated for $repo#$($pr.number) — will retry merge next cycle."
                            } catch {
                                Write-Host "Branch update failed for $repo#$($pr.number) — needs manual resolution."
                            }
                        } else {
                            throw $_
                        }
                    }

                    # Notify Discord
                    $mergePayload = @{
                        content = "<@$userId>"
                        embeds = @(@{
                            title       = "Auto-Merged: $($pr.title)"
                            description = "PR #$($pr.number) scored **$score/10** and was auto-merged.`n`n[View PR]($($pr.html_url))"
                            color       = 3066993
                            footer      = @{ text = "$repoShort | Auto-Merge" }
                            timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                        })
                    } | ConvertTo-Json -Depth 5 -Compress
                    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" `
                        -Body ([System.Text.Encoding]::UTF8.GetBytes($mergePayload))

                    # Log to decision memory
                    $memoryFile = "$HOME\.office-rag-db\decision-memory.json"
                    if (Test-Path $memoryFile) {
                        $memory = Get-Content $memoryFile | ConvertFrom-Json
                    } else {
                        $memory = @()
                    }
                    $memory = @($memory) + @{
                        decision   = "auto-merged"
                        repo       = $repoShort
                        pr_number  = [int]$pr.number
                        title      = $pr.title
                        score      = $score
                        timestamp  = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                    }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    Write-Host "Auto-merge failed for $repo#$($pr.number): $($_.Exception.Message)"
                }
            }

            $reviewed += $prKey
            Start-Sleep -Seconds 5
        }
    } catch {
        Write-Host "Error reviewing $repo : $($_.Exception.Message)"
    }
}

$reviewed | ConvertTo-Json | Set-Content -Path $reviewedFile -Encoding UTF8
