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

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

        # --- Decision Memory ---
    $memoryContext = ""
    $memoryFile = "$HOME\.office-rag-db\decision-memory.json"
    if (Test-Path $memoryFile) {
        $decisions = Get-Content $memoryFile | ConvertFrom-Json
        if ($decisions.Count -gt 0) {
            $approved = @($decisions | Where-Object { $webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.decision -eq "approved" }) | ForEach-Object { "- APPROVED: $($webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.title) ($($webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.repo))" }
            $rejected = @($decisions | Where-Object { $webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.decision -eq "rejected" }) | ForEach-Object { "- REJECTED: $($webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.title) ($($webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.repo)) — Reason: $($webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }

try {
    $officeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $suiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=20&labels=ai-suggested" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $allOfficeIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Office/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }
    $allSuiteIssues = (Invoke-RestMethod -Uri "https://api.github.com/repos/Koraji95-coder/Suite/issues?state=open&per_page=30" -Headers $headers) | ForEach-Object { "- #$($_.number): $($_.title)" }

    $officeAiCount = ($officeIssues | Measure-Object).Count
    $suiteAiCount = ($suiteIssues | Measure-Object).Count

    if ($officeAiCount -ge 20 -and $suiteAiCount -ge 20) {
        return
    }

    $allowOffice = if ($officeAiCount -lt 20) { "You MAY suggest Office issues. ($officeAiCount/20 open)" } else { "Do NOT suggest Office issues — at cap ($officeAiCount/20)." }
    $allowSuite = if ($suiteAiCount -lt 20) { "You MAY suggest Suite issues. ($suiteAiCount/20 open)" } else { "Do NOT suggest Suite issues — at cap ($suiteAiCount/20)." }

    # --- RAG: Query codebase for context ---
    $ragQueries = @(
        "What code has no error handling or missing try catch?"
        "What files have no test coverage?"
        "What API endpoints are missing input validation?"
        "What areas need better documentation?"
    )
    $ragQuery = $ragQueries | Get-Random

    $ragContext = ""
    try {
        $ragContext = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $ragQuery 2>$null | Out-String
        if ($ragContext.Length -gt 3000) {
            $ragContext = $ragContext.Substring(0, 3000)
        }
    } catch {
        $ragContext = "(RAG unavailable this cycle)"
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}
.reason)" }
            $memoryContext = "PAST DECISIONS (learn from these):`n$($approved -join "`n")`n$($rejected -join "`n")"
        }
    }

    $prompt = @"
You are an autonomous technical project manager. Your job is to find exactly 1 high-value issue to create right now.

Rules:
- Suggest exactly 1 issue. No more.
- $allowOffice
- $allowSuite
- Do NOT duplicate any existing issue below.
- Focus on: test coverage gaps, missing docs, technical debt, CI/CD hardening, or developer experience improvements.
- The issue must be specific and actionable — not vague.
- Use the CODE CONTEXT below to make your suggestion based on REAL code you can see.

EXISTING OPEN ISSUES (do not duplicate):

OFFICE:
$($allOfficeIssues -join "`n")

SUITE:
$($allSuiteIssues -join "`n")

CODE CONTEXT FROM THE CODEBASE (use this to find real problems):
$memoryContext

$ragContext

Respond ONLY with valid JSON. No markdown, no explanation:
{ "repo": "Office", "title": "issue title", "body": "2-3 sentence description referencing specific files" }
"@

    $chatBody = @{
        model    = "qwen3:14b"
        messages = @(@{ role = "user"; content = $prompt })
        stream   = $false
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
    $content = $response.message.content

    if ($content -match '\{[\s\S]*\}') {
        $jsonText = $Matches[0]
        $suggestion = $jsonText | ConvertFrom-Json

        $repo = if ($suggestion.repo -eq "Office") { "Koraji95-coder/Office" } else { "Koraji95-coder/Suite" }

        $issueBody = @{
            title  = $suggestion.title
            body   = "$($suggestion.body)`n`n---`n_Auto-created by AI pipeline (RAG-enhanced)._"
            labels = @("ai-suggested")
        } | ConvertTo-Json -Depth 3

        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues" `
            -Method POST -Headers $headers -ContentType "application/json" -Body $issueBody

        $assignBody = @{
            assignees = @("copilot-swe-agent[bot]")
        } | ConvertTo-Json -Depth 3

        try {
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/issues/$($result.number)" `
                -Method PATCH -Headers $headers -ContentType "application/json" -Body $assignBody
        } catch {}

        $colors = @{ "Office" = 16744256; "Suite" = 5793266 }
        $repoColor = if ($colors.ContainsKey($suggestion.repo)) { $colors[$suggestion.repo] } else { 8421504 }

        $payload = @{
            content = "<@$userId>"
            embeds = @(@{
                title       = "New Issue Created (RAG-Enhanced)"
                description = "**$($suggestion.title)**`n`n$($suggestion.body)`n`n[View Issue]($($result.html_url))"
                color       = $repoColor
                footer      = @{ text = "$($suggestion.repo) | ai-suggested | RAG: $ragQuery" }
                timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
            })
        }

        $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
        Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload))
    }
}
catch {
    $errBody = @{
        content = "<@$userId> **Auto-Pipeline Failed** — $($_.Exception.Message)"
    } | ConvertTo-Json -EscapeHandling EscapeNonAscii

    Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json" -Body $errBody
}


