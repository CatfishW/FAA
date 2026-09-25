@echo off
setlocal
cd /d "%~dp0"
echo Keep this window open while using terrain in FAA XR-3.
echo The simulator host must already run its read-only terrain service on port 8767.
echo Uses your own Windows OpenSSH configuration and keys. No credentials are bundled.
powershell.exe -NoProfile -Command "$h=$env:FAA_TERRAIN_SSH_HOST; if(-not $h){$h=Read-Host 'Existing SSH host alias (for example 4090)'}; if($h -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.@-]{0,199}$'){Write-Error 'Invalid SSH host alias'; exit 2}; if(-not (Get-Command ssh.exe -ErrorAction SilentlyContinue)){Write-Error 'Windows OpenSSH Client is required'; exit 2}; & ssh.exe -N -T -o ExitOnForwardFailure=yes -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -L 127.0.0.1:12679:127.0.0.1:8767 $h; exit $LASTEXITCODE"
exit /b %ERRORLEVEL%
