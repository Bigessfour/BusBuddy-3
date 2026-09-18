# UTM dev bridge (Mac Cursor → Windows 11 guest)

Cursor does not document a UTM integration. There is no official “Cursor ↔ UTM” protocol. This repo shells out to [`Scripts/utm-dev-bridge.sh`](../Scripts/utm-dev-bridge.sh). The CLI is assembled from four **documented** primitives plus **BusBuddy-only** rules.

## Agent surface

```bash
./Scripts/utm-dev-bridge.sh doctor
./Scripts/utm-dev-bridge.sh status          # reads ~/.cache/utm-dev-bridge/status.json
./Scripts/utm-dev-bridge.sh sync [--mirror]
./Scripts/utm-dev-bridge.sh exec -- <powershell>
./Scripts/utm-dev-bridge.sh test [--filter EXPR] [--no-sync] [--full] [--deps-only]
./Scripts/utm-dev-bridge.sh launch [--no-sync]
./Scripts/utm-dev-bridge.sh pull-logs
./Scripts/utm-dev-bridge.sh watch           # humans only — agents must not start this
```

Shims that must keep their filenames (done-checklist): `./run-wpf.sh` (Mac Postgres + preflight + `launch`), `./Scripts/utm-wpf-test.sh` → `test`.

Config: `~/.config/utm-dev-bridge.env` from [`Scripts/utm-dev-bridge.env.example`](../Scripts/utm-dev-bridge.env.example). `SSH_KEY=~/.ssh/busbuddy-utm` is expanded even when quoted.

## Documented primitives

### 1. UTM host control (`utmctl`)

CLI wrapper around AppleScript. Guest `ip-address` and `exec` need the QEMU guest agent.

- [UTM Scripting](https://docs.getutm.app/scripting/scripting/)
- [Scripting reference](https://docs.getutm.app/scripting/reference/) / [cheat sheet](https://docs.getutm.app/scripting/cheat-sheet/)

Hard limits UTM states:

- Guest exec/IP only work if the QEMU guest agent is running.
- `utmctl` does **not** work from an SSH session into the Mac (AppleScript needs a logged-in GUI).

This repo uses `utmctl start` / `status` / `ip-address`. **`utmctl exec` is bootstrap only** (`Scripts/Enable-BusBuddyOpenSSH.ps1`). It is the wrong channel for a visible WPF window (often Session 0 / SYSTEM).

### 2. Windows guest agent (IP + bootstrap exec)

UTM’s guest-agent pages are Linux-first. Windows 11 ARM in UTM: VirtIO ISO → `guest-agent\qemu-ga-*.msi`.

- [Fedora virtio-win](https://fedorapeople.org/groups/virt/virtio-win/direct-downloads/stable-virtio/)
- [virtio-win/kvm-guest-drivers-windows](https://github.com/virtio-win/kvm-guest-drivers-windows)

That supports `utmctl ip-address`. It does **not** give the logged-in `Macbook` desktop. SSH is the WPF channel.

### 3. Windows OpenSSH (the real Cursor path)

[`Scripts/Enable-BusBuddyOpenSSH.ps1`](../Scripts/Enable-BusBuddyOpenSSH.ps1) follows Microsoft Learn:

- [OpenSSH for Windows overview](https://learn.microsoft.com/windows-server/administration/openssh/openssh-overview)
- [Key-based authentication](https://learn.microsoft.com/windows-server/administration/openssh/openssh_keymanagement) — admin keys in `C:\ProgramData\ssh\administrators_authorized_keys`, ACL **SYSTEM + Administrators only** (this repo grants `SYSTEM` and `*S-1-5-32-544` so localized Windows still matches Learn)
- [sshd configuration](https://learn.microsoft.com/windows-server/administration/openssh/openssh-server-configuration)

Mac client: dedicated key `~/.ssh/busbuddy-utm`, `BatchMode=yes`, private `known_hosts` under `~/.cache/utm-dev-bridge/`.

### 4. PowerShell `-EncodedCommand` (UTF-16LE)

Microsoft documents `-EncodedCommand` as a Base64 **UTF-16LE** command string ([about_PowerShell_exe](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_powershell_exe)). Windows OpenSSH (`cmd.exe` quoting) eats multiline `powershell -Command`. The bridge `exec` / `test` / `launch` path **always** uses `-EncodedCommand`. Never invent `ssh … powershell -Command "…"`.

### 5. Interactive GUI launch (`schtasks /IT`)

A key-authenticated SSH session is not the logged-in desktop. [schtasks /IT](https://learn.microsoft.com/windows-server/administration/windows-commands/schtasks-create) runs the task only when `/RU` is logged on. `launch` creates `BusBuddyUtmLaunch` as `Macbook` with `/IT /RL LIMITED` and `/Run`s it against `C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe`.

## BusBuddy-only rules (not in vendor docs)

| Rule                                                                             | Why                                                                                                                                                                                  |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Runtime root is `C:\dev\BusBuddy-3`                                              | WPF cannot reliably build on UTM SPICE WebDAV `Z:\`. Share is bootstrap; `utm_run_in_vm.ps1` may robocopy Z: → NTFS once.                                                            |
| Default rsync has **no** `--delete`                                              | `--delete` wipes guest Serilog, local `appsettings`, and evidence under `C:\dev`. Humans may pass `sync --mirror` (protects `keys/`, `logs/`, `artifacts/rosters/`, `TestResults/`). |
| Exclude `keys/`, `artifacts/rosters/`, `*.log`, `logs/`, `Logs/`, `TestResults/` | Do not extra-copy roster PII or clobber guest logs.                                                                                                                                  |
| Quote `--rsync-path`                                                             | Git for Windows `C:/Program Files/Git/usr/bin/rsync.exe` has spaces.                                                                                                                 |
| No default `FORWARD_PORTS`                                                       | BusBuddy is Syncfusion WPF + Mac Docker Postgres (`192.168.64.1:5432`), not a web stack on 3000/5000/8080.                                                                           |
| Probe cached IPv4 with 2s `ssh echo ok`                                          | `utmctl ip-address` fails when the guest agent is down; yesterday’s DHCP lease is not SSH.                                                                                           |
| `watch` is human-only                                                            | Agents must not start a second ControlMaster. Read `status.json`.                                                                                                                    |
| Cursor stays on the Mac                                                          | Not Remote SSH into the guest. Composition is “run a terminal command.”                                                                                                              |

## Status file

After connect/sync the bridge writes `~/.cache/utm-dev-bridge/status.json`:

`ok`, `ip`, `ssh_user`, `remote_dir`, `win_root`, `master_pid`, `master_alive`, `last_sync`, `engine`, `rsync_remote`.

Two agent chats attach to the existing ControlMaster (`ssh -O check`) instead of deleting `ssh.sock`.

## Topology

```
Cursor (Mac desktop)
  └─ ./Scripts/utm-dev-bridge.sh {status,sync,exec,test,launch,pull-logs}
           │
           ▼
   OpenSSH on guest (user Macbook, key ~/.ssh/busbuddy-utm)
           │
           ▼
   C:\dev\BusBuddy-3   (local NTFS copy)
```

Postgres for the guest is Mac Docker. `run-wpf.sh` writes `keys/mac-host-ip.txt` (usually `192.168.64.1`).

## First-time guest

1. Install .NET 9 SDK (ARM64) + Windows Desktop runtime.
2. VirtIO guest agent if you want `utmctl ip-address`.
3. Copy `busbuddy-utm.pub` to `C:\Users\Public\busbuddy-utm.pub`.
4. From Mac GUI (not SSH-into-Mac): `utmctl exec Windows --cmd powershell.exe -- -File …\Enable-BusBuddyOpenSSH.ps1` **or** run that script elevated inside the VM.
5. `./Scripts/utm-dev-bridge.sh doctor` then `sync`.

If SSH fails, the Microsoft key-management page above is the checklist (`administrators_authorized_keys` ACL, sshd running, firewall port 22).
