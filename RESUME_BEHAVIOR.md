# Resume Behavior

## Yes, GutenbergSync supports smart resume! ✅

When you restart a sync after a failure, rsync automatically:

### 1. **Incremental Sync (Built-in)**
- **Skips files that already exist** and are up-to-date (same size and modification time)
- Only transfers **new files** or **changed files**
- This is rsync's default behavior - no re-downloading of completed files

### 2. **Partial File Resume**
- **Resumes interrupted file transfers** using `--partial` and `--append-verify` flags
- `--partial`: Keeps partially transferred files instead of deleting them
- `--append-verify`: Appends to existing partial files and verifies data integrity
- Partial files are stored in `.rsync-partial/` directory
- On resume, rsync **continues from where it left off** for partially downloaded files
- Once complete, the partial file is moved to its final location

### 3. **How It Works**

**First sync:**
```bash
./gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub
# Downloads all files
```

**If interrupted (Ctrl+C, network error, etc.):**
- Files already downloaded are **kept**
- Partially downloaded files are saved in `.rsync-partial/`

**Resume (run the same command again):**
```bash
./gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub
# Only downloads:
#   - Files that weren't downloaded yet
#   - Files that were partially downloaded (resumes from where it stopped)
#   - Files that changed on the server
# Skips files that are already complete and up-to-date
```

### 4. **Example Scenario**

```
Initial sync: 1000 files, 50GB
- Downloads 500 files (25GB)
- Interrupted (network error)

Resume sync:
- Skips the 500 files already downloaded ✅
- Resumes the 1 file that was partially downloaded ✅
- Downloads the remaining 499 files
- Total time saved: ~50% (doesn't re-download the 25GB)
```

### 5. **Verification**

You can verify resume is working by:
1. Starting a sync
2. Interrupting it (Ctrl+C) after some files download
3. Running the same command again
4. Observing that it skips already-downloaded files and only transfers what's missing

### 6. **Technical Details**

The rsync command includes:
- `--archive` (-a): Preserves file attributes, enables incremental sync
- `--partial`: Keeps partial files instead of deleting them on interruption
- `--partial-dir=.rsync-partial`: Stores partial files in hidden directory
- `--append-verify`: Resumes partial files by appending (with data verification)

These flags ensure:
- ✅ No duplicate downloads of completed files
- ✅ Resume of interrupted file transfers
- ✅ Efficient incremental updates

### 7. **Best Practices**

- **Safe to interrupt**: You can stop a sync at any time (Ctrl+C) and resume later
- **Safe to restart**: Running the same sync command multiple times is safe and efficient
- **No manual cleanup needed**: Partial files are automatically handled
- **Network interruptions**: Automatically handled - just restart the sync

