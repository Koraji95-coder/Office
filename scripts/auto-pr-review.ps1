$webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }
$reviewedFile = "$HOME\.office-rag-db\reviewed-prs.json"

# Load already-reviewed PRs
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
            # Skip if not from Copilot
            if ($pr.user.login -ne "copilot-swe-agent[bot]" -and $pr.user.login -ne "Copilot") { continue }

            # Skip if already reviewed
            $prKey = "$repo#$($pr.number)@$($pr.updated_at)"
            if ($reviewed -contains $prKey) { continue }

            # Get the diff
            $diffHeaders = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3.diff" }
            $diff = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $diffHeaders

            # Truncate diff if huge
            if ($diff.Length -gt 6000) {
                $diff = $diff.Substring(0, 6000) + "`n... (truncated)"
            }

            # RAG: find related code for context
            $ragContext = ""
            try {
                $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $pr.title 2>$null | Out-String
                if ($ragContext.Length -gt 2000) {
                    $ragContext = $ragContext.Substring(0, 2000)
                }
            } catch {}

            # Ask Ollama to review
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

            # Color based on verdict
            $color = 8421504  # gray default
            if ($review -match "APPROVE") { $color = 5793266 }       # green
            if ($review -match "REQUEST_CHANGES") { $color = 16744256 }  # orange
            if ($review -match "NEEDS_DISCUSSION") { $color = 16776960 }  # yellow

            $repoShort = $repo.Split("/")[1]

            # Post to Discord
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

            # Mark as reviewed
            $reviewed += $prKey

            # Small delay between reviews so Ollama doesn't choke
            Start-Sleep -Seconds 5
        }
    } catch {
        Write-Host "Error reviewing $repo : $($_.Exception.Message)"
    }
}

# Save reviewed list
$reviewed | ConvertTo-Json | Set-Content -Path $reviewedFile -Encoding UTF8
