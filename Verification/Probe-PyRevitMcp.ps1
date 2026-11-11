param([string]$ToolName='', [string]$ArgumentsJson='{}', [string]$ArgumentsFile='')
$ErrorActionPreference='Stop'
if($ArgumentsFile) { $ArgumentsJson=Get-Content -LiteralPath $ArgumentsFile -Raw }
$startInfo=[System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName='C:\Users\Hoa Le\AppData\Roaming\pyRevit-Master\bin\pyrevit.exe'
$startInfo.Arguments='mcp'
$startInfo.UseShellExecute=$false
$startInfo.CreateNoWindow=$true
$startInfo.RedirectStandardInput=$true
$startInfo.RedirectStandardOutput=$true
$startInfo.RedirectStandardError=$true
$process=[System.Diagnostics.Process]::new()
$process.StartInfo=$startInfo
function Send-Mcp($message) {
    $process.StandardInput.WriteLine(($message | ConvertTo-Json -Depth 30 -Compress))
    $process.StandardInput.Flush()
}
function Receive-Mcp($requestId) {
    for($i=0;$i -lt 30;$i++) {
        $read=$process.StandardOutput.ReadLineAsync()
        if(-not $read.Wait(20000)) { throw 'MCP response timeout (20 seconds).' }
        $line=$read.Result
        if($null -eq $line) { throw 'MCP server ended before responding.' }
        try { $reply=$line | ConvertFrom-Json -Depth 50 } catch { continue }
        if($reply.id -eq $requestId) { return $reply }
    }
    throw 'No matching MCP response.'
}
try {
    [void]$process.Start()
    Send-Mcp @{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='CompareProjectReadOnlyProbe';version='1.0'}}}
    $initialize=Receive-Mcp 1
    if($initialize.error) { $initialize | ConvertTo-Json -Depth 50;exit 1 }
    Send-Mcp @{jsonrpc='2.0';method='notifications/initialized'}
    if($ToolName) {
        Send-Mcp @{jsonrpc='2.0';id=2;method='tools/call';params=@{name=$ToolName;arguments=($ArgumentsJson | ConvertFrom-Json -AsHashtable)}}
    } else {
        Send-Mcp @{jsonrpc='2.0';id=2;method='tools/list';params=@{}}
    }
    Receive-Mcp 2 | ConvertTo-Json -Depth 50
} finally {
    if(-not $process.HasExited) { $process.StandardInput.Close();if(-not $process.WaitForExit(1500)) {$process.Kill()} }
    $process.Dispose()
}
