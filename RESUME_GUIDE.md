# Resume Guide - What to Do When Sync is Interrupted

## ✅ Yes, Just Run the Command Again!

**This is exactly the right approach.** When a sync is interrupted (cancelled, network error, etc.), simply run the **exact same command** again.

## What Happens When You Resume

### Scenario: Sync Interrupted After 10 Minutes

```bash
# First attempt - interrupted after 10 minutes
./gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub --metadata-only
# [Downloads some files, then cancelled]

# Second attempt - just run the same command
./gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub --metadata-only
# ✅ Automatically resumes from where it left off
```

### What rsync Does Automatically:

1. **Skips completed files**: Files that were fully downloaded are skipped (rsync checks size and modification time)
2. **Resumes partial files**: Files that were partially downloaded are resumed using delta-transfer
3. **Downloads new files**: Only files that weren't started are downloaded fresh

## Example Timeline

```
Time 0:00 - Start sync
Time 0:05 - Downloaded 1000 files completely ✅
Time 0:10 - Downloading file #1001 (50% complete) - INTERRUPTED
Time 0:15 - Run same command again
Time 0:15 - rsync: "1000 files already exist, skipping..."
Time 0:15 - rsync: "File #1001 is partial, resuming from 50%..."
Time 0:16 - rsync: "Continuing with remaining files..."
```

## No Manual Steps Required

You don't need to:
- ❌ Clean up partial files
- ❌ Check what was downloaded
- ❌ Specify resume flags
- ❌ Manually track progress

Just run the same command - rsync handles everything automatically.

## Verification

You can verify resume is working by:

1. **Check partial files** (optional):
   ```bash
   ls -lh /mnt/workspace/gutenberg/gutenberg-epub/.rsync-partial/
   ```

2. **Watch the output**: On resume, you'll see rsync skip existing files and continue with new/partial ones

3. **Check file count**: After resume completes, verify all files are present

## Best Practices

- **Safe to interrupt**: Press Ctrl+C anytime - it's safe
- **Safe to restart**: Run the same command as many times as needed
- **No cleanup needed**: Partial files are automatically managed
- **Network interruptions**: Just restart - rsync handles it

## What If Something Goes Wrong?

If you suspect issues:

1. **Check for partial files**:
   ```bash
   find /mnt/workspace/gutenberg -name ".rsync-partial" -type d
   ```

2. **Verify downloaded files**:
   ```bash
   ./gutenberg-sync audit scan --directory /mnt/workspace/gutenberg
   ```

3. **Force re-sync** (if needed):
   ```bash
   # Delete partial files to force fresh download
   rm -rf /mnt/workspace/gutenberg/gutenberg-epub/.rsync-partial/
   ./gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub --metadata-only
   ```

## Summary

**Just run the same command again** - that's it! rsync's smart resume handles everything automatically. No special flags, no manual intervention, no cleanup needed.

