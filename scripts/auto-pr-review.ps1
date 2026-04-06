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
        $prs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls?state=open&per_page=100" -Headers $headers

        foreach ($pr in $prs) {
            if ($pr.user.login -ne "copilot-swe-agent[bot]" -and $pr.user.login -ne "Copilot") { continue }

            # Skip PRs with no code changes yet (Copilot still working)
            if ($pr.additions -eq 0 -and $pr.deletions -eq 0) {
                Write-Host "SKIPPING: $repo#$($pr.number) - no code changes yet (Copilot still working)"
                continue
            }

            # Skip draft PRs with [WIP] in title (Copilot actively editing)
            if ($pr.draft -eq $true -and $pr.title -match "\[WIP\]") {
                Write-Host "SKIPPING: $repo#$($pr.number) - WIP draft, Copilot still editing"
                continue
            }

            $prKey = "$repo#$($pr.number)@$($pr.updated_at)"
            if ($reviewed -contains $prKey) { continue }

            Write-Host "`n--- Reviewing $repo#$($pr.number): $($pr.title) ---"

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

            $repoShort = $repo.Split("/")[1]

            # Determine verdict
            $verdict = "UNKNOWN"
            if ($review -match "REQUEST_CHANGES") { $verdict = "REQUEST_CHANGES" }
            elseif ($review -match "NEEDS_DISCUSSION") { $verdict = "NEEDS_DISCUSSION" }
            elseif ($review -match "APPROVE") { $verdict = "APPROVE" }

            # Extract score
            $score = 0
            if ($review -match "(\d+)\s*/\s*10") { $score = [int]$Matches[1] }

            # Submit GitHub PR review
            $ghReviewEvent = "COMMENT"
            if ($verdict -eq "APPROVE") { $ghReviewEvent = "APPROVE" }
            elseif ($verdict -eq "REQUEST_CHANGES") { $ghReviewEvent = "REQUEST_CHANGES" }

            try {
                $reviewBody = @{
                    body  = "## Auto-Review (Ollama)`n`n$review`n`n---`n*Automated review powered by qwen3:14b + RAG context*"
                    event = $ghReviewEvent
                } | ConvertTo-Json -Compress
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($reviewBody)) | Out-Null
                Write-Host "Submitted $ghReviewEvent review on $repo#$($pr.number)"
            } catch {
                Write-Host "Failed to submit review on $repo#$($pr.number): $($_.Exception.Message)"
            }

            # ---- MERGE LOGIC ----
            $mergeStatus = ""
            $shouldMerge = ($verdict -eq "APPROVE" -and $score -ge 9)

            if ($shouldMerge -and $pr.draft -eq $true) {
                Write-Host "Marking $repo#$($pr.number) as ready for review..."
                try {
                    $readyBody = @{ query = "mutation { markPullRequestReadyForReview(input: { pullRequestId: `"$($pr.node_id)`" }) { pullRequest { isDraft } } }" } | ConvertTo-Json -Compress
                    Invoke-RestMethod -Uri "https://api.github.com/graphql" -Method POST -Headers @{ Authorization = "Bearer $ghToken" } -ContentType "application/json" -Body $readyBody | Out-Null
                    Write-Host "Marked $repo#$($pr.number) as ready. Waiting for checks to initialize..."
                    Start-Sleep -Seconds 15
                } catch {
                    Write-Host "Failed to mark ready - skipping merge."
                    $mergeStatus = "Could not mark ready for review"
                    $shouldMerge = $false
                }
            }

            if ($shouldMerge) {
                # Re-fetch PR to get latest head SHA (Copilot may have pushed new commits)
                try {
                    $latestPr = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $headers
                    $headSha = $latestPr.head.sha
                } catch {
                    $headSha = $pr.head.sha
                }

                # Check CI checks
                try {
                    $checkRuns = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/commits/$headSha/check-runs" -Headers $headers
                    $pendingChecks = @($checkRuns.check_runs | Where-Object { $_.status -ne "completed" })
                    $failedChecks = @($checkRuns.check_runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -notin @("success", "neutral", "skipped") })

                    if ($pendingChecks.Count -gt 0) {
                        $pendingNames = ($pendingChecks | ForEach-Object { $_.name }) -join ", "
                        Write-Host "WAITING: $repo#$($pr.number) - checks running: $pendingNames"
                        $mergeStatus = "Waiting on checks: $pendingNames"
                        $shouldMerge = $false
                    } elseif ($failedChecks.Count -gt 0) {
                        $failedNames = ($failedChecks | ForEach-Object { "$($_.name)=$($_.conclusion)" }) -join ", "
                        Write-Host "FAILED CHECKS: $repo#$($pr.number) - $failedNames"
                        $mergeStatus = "Failed checks: $failedNames"
                        $shouldMerge = $false
                    }
                } catch {
                    Write-Host "WAITING: $repo#$($pr.number) - checks not ready yet, will retry next cycle."
                    $mergeStatus = "Checks not ready yet - will retry next cycle"
                    $shouldMerge = $false
                }
            }

            if ($shouldMerge) {
                # Check mergeability
                try {
                    $freshPr = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $headers
                } catch {
                    $freshPr = $null
                }

                if ($null -eq $freshPr -or $freshPr.mergeable -eq $false) {
                    Write-Host "CONFLICT: $repo#$($pr.number) - attempting branch update..."
                    try {
                        Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/update-branch" -Method PUT -Headers $headers -ContentType "application/json" -Body ('{"expected_head_sha":"' + $pr.head.sha + '"}') | Out-Null
                        Write-Host "Branch updated for $repo#$($pr.number) - will merge next cycle."
                        $mergeStatus = "Branch updated - will merge next cycle"
                    } catch {
                        Write-Host "MANUAL REVIEW NEEDED: $repo#$($pr.number) - branch update failed."
                        $mergeStatus = "Merge conflict - needs manual resolution"
                    }
                    $shouldMerge = $false
                }
            }

            if ($shouldMerge) {
                # Attempt merge
                $mergeBody = @{ commit_title = "auto-merge: $($pr.title) [score $score/10]"; merge_method = "squash" } | ConvertTo-Json -Compress
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/merge" -Method PUT -Headers $headers -ContentType "application/json" -Body $mergeBody | Out-Null
                    Write-Host "AUTO-MERGED: $repo#$($pr.number) - score $score/10"
                    $mergeStatus = "Auto-merged"

                    # Log to decision memory
                    $memoryFile = "$HOME\.office-rag-db\decision-memory.json"
                    if (Test-Path $memoryFile) { $memory = Get-Content $memoryFile | ConvertFrom-Json } else { $memory = @() }
                    $memory = @($memory) + @{ decision = "auto-merged"; repo = $repoShort; pr_number = [int]$pr.number; title = $pr.title; score = $score; timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ") }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    $sc = $_.Exception.Response.StatusCode
                    if ($sc -eq 409) { $mergeStatus = "Merge conflict - will retry next cycle" }
                    elseif ($sc -eq 405) { $mergeStatus = "Blocked - checks pending or ruleset" }
                    else { $mergeStatus = "Merge failed - HTTP $sc" }
                    Write-Host "$mergeStatus on $repo#$($pr.number)"
                }
            }

            # ---- DISCORD NOTIFICATION ----
            # Color is based on FINAL outcome, not just the review verdict
            if ($mergeStatus -match "Auto-merged") {
                $color = 3066993    # Teal
                $statusEmoji = "MERGED"
            } elseif ($mergeStatus -match "conflict|manual|failed") {
                $color = 15548997   # Red
                $statusEmoji = "CONFLICT"
            } elseif ($mergeStatus -match "Waiting|Blocked|checks") {
                $color = 16776960   # Yellow
                $statusEmoji = "WAITING"
            } elseif ($verdict -eq "REQUEST_CHANGES") {
                $color = 15548997   # Red
                $statusEmoji = "CHANGES REQUESTED"
            } elseif ($verdict -eq "NEEDS_DISCUSSION") {
                $color = 16776960   # Yellow
                $statusEmoji = "NEEDS DISCUSSION"
            } elseif ($verdict -eq "APPROVE") {
                $color = 5763719    # Green
                $statusEmoji = "APPROVED"
            } else {
                $color = 9807270    # Gray
                $statusEmoji = "REVIEWED"
            }

            # Build merge info line
            $mergeInfo = ""
            if ($mergeStatus -ne "") {
                $mergeInfo = "`n`n**Merge Status:** $mergeStatus"
            } elseif ($verdict -eq "APPROVE" -and $score -lt 9) {
                $mergeInfo = "`n`n**Merge Status:** Approved but score $score/10 < 9 - needs manual merge"
            }

            $embedDesc = "$review$mergeInfo`n`n[View PR]($($pr.html_url))"
            if ($embedDesc.Length -gt 4000) {
                $embedDesc = $embedDesc.Substring(0, 3950) + "`n... (truncated)`n`n[View PR]($($pr.html_url))"
            }

            $payload = @{
                content = "<@$userId>"
                embeds = @(@{
                    title       = "$statusEmoji | $($pr.title)"
                    description = $embedDesc
                    color       = $color
                    footer      = @{ text = "$repoShort | PR #$($pr.number) | Score: $score/10" }
                    timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                })
            }
            $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
            try {
                Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload)) | Out-Null
            } catch {
                Write-Host "Discord notification failed for $repo#$($pr.number): $($_.Exception.Message)"
            }

            $reviewed += $prKey
            Start-Sleep -Seconds 5
        }
    } catch {
        Write-Host "Error reviewing $repo : $($_.Exception.Message)"
    }
}

$reviewed | ConvertTo-Json | Set-Content -Path $reviewedFile -Encoding UTF8
Write-Host "`n=== Review cycle complete ==="



