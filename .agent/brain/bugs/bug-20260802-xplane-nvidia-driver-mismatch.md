# X-Plane service blocked by NVIDIA driver/library mismatch

- Date: 2026-08-02
- Severity: High
- Area: `4090` X-Plane 12 host / systemd service chain
- Status: Fixed

## Symptoms

`xplane12-simulator.service` reached `start-limit-hit`, and the dependent autoflight, data API, and tunnel services were inactive. Steam crashed while starting X-Plane, and `nvidia-smi` could not initialize NVML.

## Root cause

The running NVIDIA kernel module was version `595.71.05`, while the installed userspace NVIDIA libraries were version `595.84`. Steam logged the same API mismatch before its crash. The host had `/var/run/reboot-required`, confirming that the upgraded driver stack had not been activated. Because the GPU was shared by Xorg, GNOME, and several active Python/sglang processes, unloading the driver modules live would have been unsafe.

After the driver repair, a second service fault became visible: both `xplane12-tunnel.service` and the redundant `xplane12-tang-tunnel.service` were enabled and racing for reverse-forward port `12678`. The winning tunnel carried traffic, but the losing unit remained in an automatic restart loop.

## Repair

With explicit authorization, rebooted `4090`, allowed Steam's one-time Vulkan shader replay to complete, and restarted the X-Plane service chain. X-Plane's first post-update launch displayed a renderer initialization assertion; dismissing it allowed systemd to relaunch X-Plane successfully with the regenerated preferences. No source or preference files in the dirty X-Plane API worktree were modified.

Disabled and stopped only the redundant `xplane12-tang-tunnel.service`, leaving its unit file intact, then reset and restarted the canonical `xplane12-tunnel.service`. The canonical unit acquired port `12678` immediately and remained active without restarts.

## Verification performed

- Confirmed SSH access to `4090`.
- Confirmed the original `595.71.05` kernel module versus `595.84` userspace mismatch, `nvidia-smi` failure, Steam's matching API-mismatch log, and the reboot-required marker.
- After reboot, confirmed kernel `7.0.0-28-generic`, loaded NVIDIA module `595.84`, successful `nvidia-smi` detection of the RTX 4090, and removal of the reboot-required marker.
- Confirmed `xplane12-simulator`, `xplane12-autoflight`, `xplane12-data-api`, and canonical `xplane12-tunnel` are active and running; the duplicate tunnel unit is disabled and inactive.
- Confirmed `/health` reports `status=ok`, sender `webapi`, 443 subscriptions, fresh packets, and no error.
- Confirmed an airborne `/v1/snapshot` with nonzero position, altitude, indicated airspeed, storm weather, and live multiplayer/TCAS targets.
- Confirmed both PNG render routes return valid `420x480` images through the repaired tunnel.

## Prevention

After NVIDIA package upgrades, schedule a coordinated reboot before starting GPU applications. Add a service preflight that compares the loaded module version with the userspace NVML version and emits an actionable failure before Steam/X-Plane enters a restart loop. Keep exactly one enabled reverse-tunnel unit per remote port and include duplicate-listener detection in host diagnostics.
