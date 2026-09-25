param([string]$SshAlias)
$ErrorActionPreference = 'Stop'
if (-not $SshAlias) { $SshAlias = Read-Host 'Existing SSH alias for your authorized simulator/relay host' }
if ($SshAlias -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$') { throw 'Use an existing SSH config alias, not a password, command or URL.' }
Write-Host 'This is an explicit optional fallback connection, not local X-Plane detection.'
Write-Host 'Only localhost ports 12678 (flight API) and 12679 (terrain) are forwarded.'
Write-Host 'Keep this window open. SSH uses your existing account/key and host-verification settings.'
& ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=20 -o ServerAliveCountMax=3 -L 127.0.0.1:12678:127.0.0.1:12678 -L 127.0.0.1:12679:127.0.0.1:8767 $SshAlias
exit $LASTEXITCODE
