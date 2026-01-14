# Running GutenbergSync in the Background

When running long sync operations, you want the process to continue even if:
- Your screen locks
- Your terminal disconnects
- Your SSH session ends
- Your laptop goes to sleep (if on a server)

## Solution 1: nohup (Simplest)

Run the command with `nohup` to ignore hangup signals:

```bash
# Run with nohup and redirect output
nohup ./gutenberg-sync sync -t /data/gutenberg -p text-epub --auto-retry > sync.log 2>&1 &

# Check progress
tail -f sync.log

# Check if still running
ps aux | grep gutenberg-sync
```

**Pros:**
- Simple, no setup needed
- Works immediately

**Cons:**
- Harder to monitor progress
- Output goes to log file

## Solution 2: screen (Recommended)

Use `screen` to create a detachable session:

```bash
# Start a new screen session
screen -S gutenberg-sync

# Run your sync command
./gutenberg-sync sync -t /data/gutenberg -p text-epub --auto-retry

# Detach: Press Ctrl+A, then D
# Reattach: screen -r gutenberg-sync
```

**Useful screen commands:**
```bash
# List sessions
screen -ls

# Attach to session
screen -r gutenberg-sync

# Create new session with name
screen -S my-sync

# Detach: Ctrl+A then D
# Kill session: Ctrl+A then K (or exit normally)
```

**Pros:**
- Can detach and reattach
- See live output
- Multiple sessions possible

**Cons:**
- Need to install screen (usually pre-installed on Linux)

## Solution 3: tmux (Advanced)

Similar to screen but more powerful:

```bash
# Start tmux session
tmux new -s gutenberg-sync

# Run your command
./gutenberg-sync sync -t /data/gutenberg -p text-epub --auto-retry

# Detach: Ctrl+B then D
# Reattach: tmux attach -t gutenberg-sync
```

**Pros:**
- More features than screen
- Better for multiple windows/panes

**Cons:**
- May need installation
- Slightly more complex

## Solution 4: systemd Service (Best for Servers)

Create a systemd service for automatic startup and management:

### Create service file

```bash
sudo nano /etc/systemd/system/gutenberg-sync.service
```

Add this content (adjust paths as needed):

```ini
[Unit]
Description=GutenbergSync Archive Synchronization
After=network.target

[Service]
Type=simple
User=your-username
WorkingDirectory=/path/to/gutenberg-sync/publish
ExecStart=/path/to/gutenberg-sync/publish/gutenberg-sync sync -t /data/gutenberg -p text-epub --auto-retry
Restart=on-failure
RestartSec=30
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

### Enable and start

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service (start on boot)
sudo systemctl enable gutenberg-sync

# Start service
sudo systemctl start gutenberg-sync

# Check status
sudo systemctl status gutenberg-sync

# View logs
sudo journalctl -u gutenberg-sync -f
```

**Pros:**
- Automatic restart on failure
- Starts on boot
- Managed by systemd
- Logs to journal

**Cons:**
- Requires root/sudo
- More setup

## Recommended Approach

For most users, **use screen with auto-retry**:

```bash
# Start screen session
screen -S gutenberg

# Run with auto-retry (no timeout = runs indefinitely)
./gutenberg-sync sync -t /data/gutenberg -p text-epub --auto-retry

# Detach: Ctrl+A, D
# Reattach later: screen -r gutenberg
```

## Important Notes

1. **Use `--auto-retry`**: This ensures the sync restarts if interrupted
2. **No timeout by default**: Content sync now has no timeout (runs indefinitely)
3. **Resume is automatic**: rsync automatically resumes from where it stopped
4. **Check logs**: Monitor progress via logs or reattach to screen session

## Troubleshooting

### Process keeps stopping

If the process stops even in background:

1. **Check for timeout**: Use `--timeout 0` to disable timeout
2. **Check system limits**: `ulimit -a` to see process limits
3. **Check disk space**: `df -h` to ensure enough space
4. **Check network**: Ensure stable network connection

### Can't reattach to screen

```bash
# List all screen sessions
screen -ls

# Force attach (if session is attached elsewhere)
screen -r -d gutenberg-sync

# Kill stuck session
screen -X -S gutenberg-sync quit
```

### Check if sync is still running

```bash
# Check process
ps aux | grep gutenberg-sync

# Check recent activity (if using logs)
tail -f sync.log

# Check disk activity (indicates file transfers)
iostat -x 1
```

