@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $u='http://127.0.0.1:12679'; $p=Join-Path (Get-Location) 'FAA-XR3_Data\StreamingAssets\FAA\TerrainConnection.json'; if(Test-Path $p){ $c=Get-Content -Raw $p | ConvertFrom-Json; if($c.terrainUrl){$u=$c.terrainUrl} }; if($env:FAA_TERRAIN_URL){$u=$env:FAA_TERRAIN_URL}; $uri=[Uri]$u; if(-not $uri.IsAbsoluteUri -or $uri.Scheme -notin @('http','https') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment){throw 'Invalid terrain URL'}; $h=Invoke-RestMethod -TimeoutSec 8 -Uri ($u.TrimEnd('/')+'/health'); if($h.status -ne 'ready' -or $h.source_kind -ne 'xplane_dsf_elevation'){throw 'Unexpected terrain service'}; Write-Host 'Terrain elevation service is ready.'; exit 0 } catch { Write-Host 'Terrain connection unavailable. Run Start-Terrain-Tunnel.cmd or configure TerrainConnection.json / FAA_TERRAIN_URL.'; exit 2 }"
exit /b %ERRORLEVEL%
