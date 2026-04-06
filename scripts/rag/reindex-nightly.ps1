# RAG Re-Index — runs nightly to keep codebase index current
$logFile = "$HOME\.office-rag-db\reindex.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

try {
    $output = python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\index.py" 2>&1 | Out-String
    "$timestamp | SUCCESS | $output" | Out-File -Append -FilePath $logFile
} catch {
    "$timestamp | FAILED | $($_.Exception.Message)" | Out-File -Append -FilePath $logFile
}
