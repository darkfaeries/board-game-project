#!/bin/bash
# Administrator-only bootstrap. Run from the directory containing these files.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
umask 077
[[ $EUID == 0 && -d /etc/game2 && ! -L /etc/game2 ]] || exit 77
cd "$(dirname "$0")"
sh -n game2-ssh-command
sh -n game2-logs
bash -n game2-deploy
install -d -m 755 /usr/local/libexec
install -m 755 game2-ssh-command /usr/local/libexec/game2-ssh-command
install -m 700 game2-deploy /usr/local/sbin/game2-deploy
install -m 700 game2-logs /usr/local/sbin/game2-logs
install -m 600 backend.compose.yaml /etc/game2/backend.compose.yaml

python3 - <<'PY'
import pathlib, secrets, subprocess, json
root = pathlib.Path('/etc/game2')
target = root/'backend.env'
if not target.exists():
    values = dict(line.split('=', 1) for line in (root/'.env').read_text().splitlines() if '=' in line)
    password = values['GAME2_APP_PASSWORD']
    assert len(password) == 64 and all(c in '0123456789abcdef' for c in password)
    with target.open('x') as stream:
        stream.write('ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=game2;Username=game2_app;Password='+password+';Maximum Pool Size=10\n')
        stream.write('Jwt__Key='+secrets.token_hex(48)+'\n')
        stream.write('Jwt__Issuer=Game2\nJwt__Audience=Game2Clients\nDatabaseProvider=PostgreSQL\n')
    target.chmod(0o600)
bridges=[]
for name in ('game2_frontend', 'game2_private'):
    network=json.loads(subprocess.check_output(['docker','network','inspect',name]))[0]
    bridge=network.get('Options',{}).get('com.docker.network.bridge.name') or 'br-'+network['Id'][:12]
    assert bridge.startswith('br-') and len(bridge)==15
    bridges.append(bridge)
interfaces=', '.join('"'+bridge+'"' for bridge in bridges)
rules='table inet game2_guard\nflush table inet game2_guard\n'
rules+='table inet game2_guard {\n chain input {\n type filter hook input priority -10; policy accept;\n'
rules+=' iifname { '+interfaces+' } ct state established,related accept\n'
rules+=' iifname { '+interfaces+' } counter reject with icmpx type admin-prohibited\n }\n'
rules+=' chain forward {\n type filter hook forward priority -10; policy accept;\n'
rules+=' iifname { '+interfaces+' } ct state established,related accept\n'
for bridge in bridges:
    rules+=' iifname "'+bridge+'" oifname != "'+bridge+'" counter reject with icmpx type admin-prohibited\n'
rules+=' }\n}\n'
(root/'network-guard.nft').write_text(rules)
PY
nft -c -f /etc/game2/network-guard.nft
cat > /etc/systemd/system/game2-network-guard.service <<'UNIT'
[Unit]
Description=Network isolation for game2 containers only
Before=docker.service
After=local-fs.target

[Service]
Type=oneshot
ExecStart=/usr/sbin/nft -f /etc/game2/network-guard.nft
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
RequiredBy=docker.service
UNIT
systemctl daemon-reload
systemctl enable --now game2-network-guard.service

install -d -m 755 -o root -g root /var/lib/game2-access /etc/ssh/authorized_keys
for account in game2-deploy game2-logs; do
  if ! id "$account" >/dev/null 2>&1; then
    useradd --system --no-create-home --home-dir /var/lib/game2-access --shell /bin/sh "$account"
  fi
  # Existing accounts must not have elevated supplementary groups.
  [[ $(id -nG "$account") == "$account" ]] || { echo "Unexpected groups for $account" >&2; exit 1; }
  if [[ ! -e /etc/ssh/authorized_keys/$account ]]; then
    install -m 644 -o root -g root /dev/null "/etc/ssh/authorized_keys/$account"
  fi
done
cat > /etc/sudoers.d/game2-access <<'SUDO'
Defaults:game2-deploy,game2-logs env_reset
game2-deploy ALL=(root) NOPASSWD: /usr/local/sbin/game2-deploy *
game2-logs ALL=(root) NOPASSWD: /usr/local/sbin/game2-logs ""
SUDO
chmod 440 /etc/sudoers.d/game2-access
visudo -cf /etc/sudoers.d/game2-access
cat > /etc/ssh/sshd_config.d/60-game2-access.conf <<'SSH'
Match User game2-deploy,game2-logs
    AuthorizedKeysFile /etc/ssh/authorized_keys/%u
    ForceCommand /usr/local/libexec/game2-ssh-command
    DisableForwarding yes
    PermitTTY no
    PermitUserRC no
    PasswordAuthentication no
    KbdInteractiveAuthentication no
Match all
SSH
sshd -t
systemctl reload ssh.service
echo 'game2 isolation and restricted accounts installed; no application replacement performed'
