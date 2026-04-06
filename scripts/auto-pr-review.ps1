# ============================================================
# AUTO-PR-REVIEW v2 — Phase 1 Scoring Engine
# ============================================================

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
$memoryFile = "$HOME\.office-rag-db\decision-memory.json"

if (Test-Path $reviewedFile) {
    $reviewed = Get-Content $reviewedFile | ConvertFrom-Json
} else {
    $reviewed = @()
}

# Load decision memory for duplicate detection
$recentMerges = @()
if (Test-Path $memoryFile) {
    try {
        $allMemory = Get-Content $memoryFile | ConvertFrom-Json
        $cutoff = (Get-Date).ToUniversalTime().AddHours(-4)
        $recentMerges = @($allMemory | Where-Object {
            $_.decision -eq "auto-merged" -and
            [DateTime]::Parse($_.timestamp).ToUniversalTime() -gt $cutoff
        })
    } catch {}
}

$repos = @("Koraji95-coder/Office")

foreach ($repo in $repos) {
    try {
        $prs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls?state=open&per_page=100" -Headers $headers

        # Pre-fetch all open PR files for overlap detection
        $openPrFiles = @{}
        foreach ($p in $prs) {
            try {
                $files = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($p.number)/files?per_page=100" -Headers $headers
                $openPrFiles[$p.number] = @($files | ForEach-Object { $_.filename })
            } catch {}
        }

        foreach ($pr in $prs) {
            if ($pr.user.login -ne "copilot-swe-agent[bot]" -and $pr.user.login -ne "Copilot") { continue }

            $repoShort = $repo.Split("/")[1]

            # ========== GATE 1: Still a draft ==========
            if ($pr.draft -eq $true) {
                # Fetch full PR to get accurate additions count (list endpoint returns 0)
                $draftDetail = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $headers
                # Use updated_at vs created_at difference from GitHub (avoids local clock skew)
                $createdAt = [DateTime]::Parse($draftDetail.created_at)
                $updatedAt = [DateTime]::Parse($draftDetail.updated_at)
                $ageMinutes = ($updatedAt - $createdAt).TotalMinutes
                # If has code and either 10+ min old OR commits > 1 (Copilot pushed final code)
                if ($draftDetail.additions -gt 0 -and ($ageMinutes -gt 10 -or $draftDetail.commits -gt 1)) {
                    Write-Host "PROMOTE: $repoShort#$($pr.number) - draft with code, $([int]$age.TotalMinutes)m old, marking ready..."
                    try {
                        $nodeId = $draftDetail.node_id
                        $gqlHeaders = @{ Authorization = "Bearer $ghToken"; "Content-Type" = "application/json" }
                        $gqlBody = @{ query = "mutation { markPullRequestReadyForReview(input: {pullRequestId: `"$nodeId`"}) { pullRequest { number } } }" } | ConvertTo-Json -Compress
                        Invoke-RestMethod -Uri "https://api.github.com/graphql" -Method POST -Headers $gqlHeaders -Body $gqlBody | Out-Null
                        Write-Host "  -> Marked ready, will review next cycle"
                    } catch {
                        Write-Host "  -> Failed to mark ready, skipping"
                    }
                    continue
                }
                Write-Host "SKIP: $repoShort#$($pr.number) - draft (Copilot still working)"
                continue
            }

            # ========== GATE 2: No code yet ==========
            if ($pr.additions -eq 0 -and $pr.deletions -eq 0) {
                Write-Host "SKIP: $repoShort#$($pr.number) - no code changes yet"
                continue
            }

            # ========== GATE 3: Get fresh PR state ==========
            try {
                $freshPr = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $headers
            } catch {
                Write-Host "SKIP: $repoShort#$($pr.number) - could not fetch PR details"
                continue
            }

            # ========== GATE 4: Check if CI checks are done ==========
            $headSha = $freshPr.head.sha
            try {
                $checkRuns = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/commits/$headSha/check-runs" -Headers $headers
                if ($checkRuns.total_count -gt 0) {
                    $pending = @($checkRuns.check_runs | Where-Object { $_.status -ne "completed" })
                    $failed = @($checkRuns.check_runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -notin @("success", "neutral", "skipped") })

                    if ($pending.Count -gt 0) {
                        $names = ($pending | ForEach-Object { $_.name }) -join ", "
                        Write-Host "SKIP: $repoShort#$($pr.number) - checks running: $names"
                        continue
                    }
                    if ($failed.Count -gt 0) {
                        $names = ($failed | ForEach-Object { "$($_.name)=$($_.conclusion)" }) -join ", "
                        Write-Host "SKIP: $repoShort#$($pr.number) - checks failed: $names"
                        continue
                    }
                }
            } catch {}

            # ========== GATE 5: Check mergeability ==========
            if ($freshPr.mergeable -eq $false) {
                Write-Host "SKIP: $repoShort#$($pr.number) - merge conflict, attempting branch update..."
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/update-branch" -Method PUT -Headers $headers -ContentType "application/json" -Body ('{"expected_head_sha":"' + $freshPr.head.sha + '"}') | Out-Null
                    Write-Host "  -> Branch updated, will review next cycle"
                } catch {
                    Write-Host "  -> Branch update failed, needs manual resolution"
                }
                continue
            }

            # ========== GATE 6: Already reviewed this version ==========
            $prKey = "$repo#$($pr.number)@$($freshPr.updated_at)"
            if ($reviewed -contains $prKey) { continue }

            # ========== GATE 6B: Already has a review from us ==========
            try {
                $existingReviews = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" -Headers $headers
                $ourReview = $existingReviews | Where-Object { $_.user.login -eq "Koraji95-coder" -and $_.body -match "Auto-Review" }
                if ($ourReview) {
                    Write-Host "SKIP: $repoShort#$($pr.number) - already reviewed (score locked)"
                    continue
                }
            } catch {}

            # ========== GATE 7: Duplicate detection ==========
            $thisFiles = $openPrFiles[$pr.number]
            $isDuplicate = $false
            $duplicateOf = ""

            if ($thisFiles -and $recentMerges.Count -gt 0) {
                foreach ($merged in $recentMerges) {
                    if ($merged.files -and $merged.files.Count -gt 0) {
                        $overlap = @($thisFiles | Where-Object { $merged.files -contains $_ })
                        $overlapPct = if ($thisFiles.Count -gt 0) { $overlap.Count / $thisFiles.Count } else { 0 }
                        if ($overlapPct -ge 0.5) {
                            $isDuplicate = $true
                            $duplicateOf = "$($merged.repo)#$($merged.pr_number) ($($merged.title))"
                            break
                        }
                    }
                }
            }

            if ($isDuplicate) {
                Write-Host "DUPLICATE: $repoShort#$($pr.number) - 50%+ file overlap with recently merged $duplicateOf"
                try {
                    $closeBody = @{
                        body  = "## Auto-Review: DUPLICATE DETECTED`n`nThis PR overlaps 50%+ with recently merged $duplicateOf.`nClosing to prevent duplicate merges.`n`n---`n*Automated by scoring engine v2*"
                        event = "COMMENT"
                    } | ConvertTo-Json -Compress
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($closeBody)) | Out-Null
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Method PATCH -Headers $headers -ContentType "application/json" -Body '{"state":"closed"}' | Out-Null
                    Write-Host "  -> Closed as duplicate"

                    # Log it
                    if (Test-Path $memoryFile) { $memory = Get-Content $memoryFile | ConvertFrom-Json } else { $memory = @() }
                    $memory = @($memory) + @{ decision = "closed-duplicate"; repo = $repoShort; pr_number = [int]$pr.number; title = $pr.title; duplicate_of = $duplicateOf; files = $thisFiles; timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ") }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    Write-Host "  -> Failed to close duplicate"
                }
                $reviewed += $prKey
                continue
            }

            # ========== GATE 8: File overlap warning with other open PRs ==========
            $overlapWarnings = @()
            foreach ($otherNum in $openPrFiles.Keys) {
                if ($otherNum -eq $pr.number) { continue }
                $otherFiles = $openPrFiles[$otherNum]
                if ($thisFiles -and $otherFiles) {
                    $shared = @($thisFiles | Where-Object { $otherFiles -contains $_ })
                    if ($shared.Count -gt 0) {
                        $overlapWarnings += "PR #$otherNum shares $($shared.Count) file(s): $($shared[0..2] -join ', ')"
                    }
                }
            }

            # ==========================================
            # ALL GATES PASSED - PR is ready for review
            # ==========================================
            Write-Host "`n--- Reviewing $repoShort#$($pr.number): $($pr.title) ---"

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

            # Build merge history context for LLM
            $mergeHistoryContext = ""
            if ($recentMerges.Count -gt 0) {
                $mergeHistoryContext = "`nRECENTLY MERGED PRs (last 4 hours):`n"
                foreach ($m in ($recentMerges | Select-Object -Last 10)) {
                    $mergeHistoryContext += "- $($m.repo)#$($m.pr_number): $($m.title) [score $($m.score)/10]`n"
                }
            }

            # Build overlap context
            $overlapContext = ""
            if ($overlapWarnings.Count -gt 0) {
                $overlapContext = "`nFILE OVERLAP WARNINGS:`n" + ($overlapWarnings -join "`n")
            }

            $reviewPrompt = @"
You are a strict senior code reviewer. You must be critical and honest. Do NOT rubber-stamp.

PR Title: $($pr.title)
PR Description: $($pr.body)
Changed files: $($freshPr.changed_files) | Additions: $($freshPr.additions) | Deletions: $($freshPr.deletions)

DIFF:
$diff

RELATED CODEBASE CONTEXT:
$ragContext
$mergeHistoryContext
$overlapContext

SCORING RULES — follow these strictly:
- 9-10: Exceptional. Clean code, good tests, no issues, adds real value. Rare.
- 7-8: Good. Minor issues but solid contribution. Most decent PRs land here.
- 5-6: Mediocre. Missing tests, incomplete, or questionable approach.
- 3-4: Poor. Breaks things, duplicates existing code, or wrong approach entirely.
- 1-2: Reject. Empty, broken, or harmful.

DUPLICATE CHECK: If this PR does the same thing as a recently merged PR listed above, score it 1-2 and verdict REQUEST_CHANGES.

Provide your review:
1. **Verdict**: APPROVE, REQUEST_CHANGES, or NEEDS_DISCUSSION
2. **Summary**: 2-3 sentences on what this PR does
3. **Concerns**: Any issues found (or "None")
4. **Quality**: Rate 1-10 using the scoring rules above — be honest, not generous
"@

            $chatBody = @{
                model    = "qwen3:14b"
                messages = @(@{ role = "user"; content = $reviewPrompt })
                stream   = $false
            } | ConvertTo-Json -Depth 3

            $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
            $review = $response.message.content

            # Determine verdict
            $verdict = "UNKNOWN"
            if ($review -match "REQUEST_CHANGES") { $verdict = "REQUEST_CHANGES" }
            elseif ($review -match "NEEDS_DISCUSSION") { $verdict = "NEEDS_DISCUSSION" }
            elseif ($review -match "APPROVE") { $verdict = "APPROVE" }

            # Extract score
            $score = 0
            if ($review -match "(\d+)\s*/\s*10") { $score = [int]$Matches[1] }

            # ========== SCORING TIERS ==========
            # Verdict from LLM always wins — tiers only decide merge behavior
            $ghReviewEvent = "COMMENT"
            $tierAction = ""

            # If LLM explicitly said REQUEST_CHANGES, respect that regardless of score
            if ($verdict -eq "REQUEST_CHANGES") {
                $ghReviewEvent = "REQUEST_CHANGES"
                $tierAction = "request-changes"
            } elseif ($score -ge 8 -and $verdict -eq "APPROVE") {
                $ghReviewEvent = "APPROVE"
                $tierAction = "auto-merge"
            } elseif ($score -ge 6 -and $verdict -eq "APPROVE") {
                $ghReviewEvent = "APPROVE"
                $tierAction = "manual-merge"
            } elseif ($score -ge 4) {
                # Score 4-7 but not APPROVE = needs work
                $ghReviewEvent = "COMMENT"
                $tierAction = "needs-attention"
            } else {
                # Score 1-3 = auto-close
                $tierAction = "auto-close"
            }

            # Add overlap warnings to review body
            $overlapNote = ""
            if ($overlapWarnings.Count -gt 0) {
                $overlapNote = "`n`n**File Overlap Detected:**`n" + ($overlapWarnings -join "`n")
            }

            # Submit GitHub PR review
            try {
                $reviewBody = @{
                    body  = "## Auto-Review (Ollama) — Score: $score/10`n`n$review$overlapNote`n`n---`n*Automated review powered by qwen3:14b + RAG context | Scoring Engine v2*"
                    event = $ghReviewEvent
                } | ConvertTo-Json -Compress
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($reviewBody)) | Out-Null
                Write-Host "Submitted $ghReviewEvent review on $repoShort#$($pr.number) [score $score/10, tier: $tierAction]"
            } catch {
                Write-Host "Failed to submit review on $repoShort#$($pr.number): $($_.Exception.Message)"
            }

            # ========== ACT ON TIER ==========
            $mergeStatus = ""

            if ($tierAction -eq "auto-merge" -and $verdict -eq "APPROVE") {
                $mergeBody = @{ commit_title = "auto-merge: $($pr.title) [score $score/10]"; merge_method = "squash" } | ConvertTo-Json -Compress
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/merge" -Method PUT -Headers $headers -ContentType "application/json" -Body $mergeBody | Out-Null
                    Write-Host "AUTO-MERGED: $repoShort#$($pr.number) - score $score/10"
                    $mergeStatus = "Auto-merged"

                    # Log to decision memory with file list
                    if (Test-Path $memoryFile) { $memory = Get-Content $memoryFile | ConvertFrom-Json } else { $memory = @() }
                    $memory = @($memory) + @{
                        decision  = "auto-merged"
                        repo      = $repoShort
                        pr_number = [int]$pr.number
                        title     = $pr.title
                        score     = $score
                        files     = $thisFiles
                        summary   = ($review -split "`n")[0..2] -join " "
                        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                    }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    $sc = $_.Exception.Response.StatusCode
                    if ($sc -eq 409) { $mergeStatus = "Merge conflict on final attempt" }
                    elseif ($sc -eq 405) { $mergeStatus = "Blocked by ruleset" }
                    else { $mergeStatus = "Merge failed - HTTP $sc" }
                    Write-Host "$mergeStatus on $repoShort#$($pr.number)"
                }
            } elseif ($tierAction -eq "manual-merge") {
                $mergeStatus = "Score $score/10 — approved, needs manual merge"
                Write-Host "MANUAL: $repoShort#$($pr.number) - $mergeStatus"
            } elseif ($tierAction -eq "request-changes") {
                $mergeStatus = "Score $score/10 — changes requested"
                Write-Host "CHANGES: $repoShort#$($pr.number) - $mergeStatus"
            } elseif ($tierAction -eq "needs-attention") {
                $mergeStatus = "Score $score/10 — needs attention, not approved"
                Write-Host "ATTENTION: $repoShort#$($pr.number) - $mergeStatus"
            } elseif ($tierAction -eq "auto-close") {
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Method PATCH -Headers $headers -ContentType "application/json" -Body '{"state":"closed"}' | Out-Null
                    $mergeStatus = "Score $score/10 — auto-closed (low quality)"
                    Write-Host "CLOSED: $repoShort#$($pr.number) - $mergeStatus"

                    if (Test-Path $memoryFile) { $memory = Get-Content $memoryFile | ConvertFrom-Json } else { $memory = @() }
                    $memory = @($memory) + @{
                        decision  = "auto-closed"
                        repo      = $repoShort
                        pr_number = [int]$pr.number
                        title     = $pr.title
                        score     = $score
                        files     = $thisFiles
                        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                    }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    Write-Host "Failed to close $repoShort#$($pr.number)"
                }
            }

            # ---- DISCORD ----
            if ($mergeStatus -match "Auto-merged") {
                $color = 3066993; $statusLabel = "MERGED"
            } elseif ($mergeStatus -match "auto-closed") {
                $color = 10038562; $statusLabel = "AUTO-CLOSED"
            } elseif ($mergeStatus -match "conflict|failed|Blocked") {
                $color = 15548997; $statusLabel = "MERGE FAILED"
            } elseif ($verdict -eq "REQUEST_CHANGES") {
                $color = 15548997; $statusLabel = "CHANGES REQUESTED"
            } elseif ($verdict -eq "NEEDS_DISCUSSION") {
                $color = 16776960; $statusLabel = "NEEDS DISCUSSION"
            } elseif ($tierAction -eq "needs-attention") {
                $color = 16776960; $statusLabel = "NEEDS ATTENTION"
            } elseif ($tierAction -eq "manual-merge") {
                $color = 16744192; $statusLabel = "NEEDS MANUAL MERGE"
            } elseif ($verdict -eq "APPROVE") {
                $color = 5763719; $statusLabel = "APPROVED"
            } else {
                $color = 9807270; $statusLabel = "REVIEWED"
            }

            $mergeInfo = ""
            if ($mergeStatus -ne "") { $mergeInfo = "`n`n**Merge Status:** $mergeStatus" }

            $embedDesc = "$review$overlapNote$mergeInfo`n`n[View PR]($($pr.html_url))"
            if ($embedDesc.Length -gt 4000) {
                $embedDesc = $embedDesc.Substring(0, 3950) + "`n... (truncated)`n`n[View PR]($($pr.html_url))"
            }

            $payload = @{
                content = "<@$userId>"
                embeds = @(@{
                    title       = "$statusLabel | $($pr.title)"
                    description = $embedDesc
                    color       = $color
                    footer      = @{ text = "$repoShort | PR #$($pr.number) | Score: $score/10 | Tier: $tierAction" }
                    timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                })
            }
            $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
            try {
                Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload)) | Out-Null
            } catch {
                Write-Host "Discord failed for $repoShort#$($pr.number): $($_.Exception.Message)"
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




