# Fixing Permission Issues

## Problem
The application needs write access to `/mnt/workspace/gutenberg` to create the database file (`gutenberg.db`).

## Solution Options

### Option 1: Create Directory with Proper Permissions (Recommended)

```bash
# Create the directory
sudo mkdir -p /mnt/workspace/gutenberg

# Set ownership to your user
sudo chown -R $USER:$USER /mnt/workspace/gutenberg

# Set permissions (read/write/execute for owner, read/execute for group/others)
sudo chmod -R 755 /mnt/workspace/gutenberg
```

**Or if you want full control:**
```bash
sudo mkdir -p /mnt/workspace/gutenberg
sudo chown -R $USER:$USER /mnt/workspace/gutenberg
sudo chmod -R 775 /mnt/workspace/gutenberg
```

### Option 2: Use a Different Database Location

Create a config file that specifies a database path you have access to:

```bash
# Create config in your home directory or current directory
./gutenberg-sync config init --path ./config.json
```

Then edit `config.json` to set a database path you can write to:

```json
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/gutenberg"
  },
  "Catalog": {
    "DatabasePath": "/home/yourusername/gutenberg.db"
  }
}
```

### Option 3: Create Parent Directory Structure

If `/mnt/workspace` doesn't exist or you don't have access:

```bash
# Check if /mnt/workspace exists
ls -la /mnt/workspace

# If it doesn't exist, create it
sudo mkdir -p /mnt/workspace
sudo chown -R $USER:$USER /mnt/workspace
sudo chmod -R 755 /mnt/workspace

# Then create the gutenberg directory
mkdir -p /mnt/workspace/gutenberg
```

### Option 4: Use a Directory You Own

If you can't modify `/mnt/workspace`, use a directory in your home:

```bash
# Use a directory in your home
./gutenberg-sync sync -t ~/gutenberg -p text-epub
```

## Quick Fix Command

**Most common solution - run this:**
```bash
sudo mkdir -p /mnt/workspace/gutenberg && \
sudo chown -R $USER:$USER /mnt/workspace/gutenberg && \
sudo chmod -R 755 /mnt/workspace/gutenberg
```

## Verify Permissions

After fixing, verify you can write:
```bash
# Test write access
touch /mnt/workspace/gutenberg/test.txt && rm /mnt/workspace/gutenberg/test.txt && echo "✓ Write access OK"
```

## Alternative: Set Database Path in Config

If you want to keep the sync directory at `/mnt/workspace/gutenberg` but put the database elsewhere:

```bash
# Create config
./gutenberg-sync config init

# Edit config.json to add:
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/gutenberg"
  },
  "Catalog": {
    "DatabasePath": "/home/$USER/.gutenberg-sync/gutenberg.db"
  }
}

# Create the database directory
mkdir -p ~/.gutenberg-sync
```

