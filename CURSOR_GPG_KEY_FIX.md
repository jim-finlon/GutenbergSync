# Cursor GPG Key Fix - Quick Reference

## Problem
When running `sudo apt-get update`, you get this error:
```
W: GPG error: https://downloads.cursor.com/aptrepo stable InRelease: The following signatures couldn't be verified because the public key is not available: NO_PUBKEY 42A1772E62E492D6
E: The repository 'https://downloads.cursor.com/aptrepo stable InRelease' is not signed.
```

## Quick Fix

Run these commands to download and install the GPG key from the working alternate URL:

```bash
# Download the key to a temporary location
curl -fsSL https://downloads.cursor.com/keys/anysphere.asc -o /tmp/cursor-key.asc

# Convert to binary keyring format and install
gpg --dearmor < /tmp/cursor-key.asc > /tmp/cursor.gpg
sudo cp /tmp/cursor.gpg /usr/share/keyrings/cursor.gpg
sudo chmod 644 /usr/share/keyrings/cursor.gpg

# Update repository config to use the keyring (if not already set)
sudo sed -i 's|deb \[arch=amd64,arm64\]|deb [arch=amd64,arm64 signed-by=/usr/share/keyrings/cursor.gpg]|' /etc/apt/sources.list.d/cursor.list

# Uncomment the repository if it was commented out
sudo sed -i 's/^#deb/deb/' /etc/apt/sources.list.d/cursor.list

# Test the fix
sudo apt-get update
```

## One-Liner Version

If you prefer a single command:

```bash
curl -fsSL https://downloads.cursor.com/keys/anysphere.asc | gpg --dearmor | sudo tee /usr/share/keyrings/cursor.gpg > /dev/null && sudo chmod 644 /usr/share/keyrings/cursor.gpg && sudo sed -i 's|deb \[arch=amd64,arm64\]|deb [arch=amd64,arm64 signed-by=/usr/share/keyrings/cursor.gpg]|' /etc/apt/sources.list.d/cursor.list && sudo sed -i 's/^#deb/deb/' /etc/apt/sources.list.d/cursor.list && sudo apt-get update
```

## Verification

Verify the key is installed correctly:

```bash
# Check key file exists and has content (should be ~1.2KB, not 0 bytes)
ls -lh /usr/share/keyrings/cursor.gpg

# Verify key ID matches
gpg --no-default-keyring --keyring /usr/share/keyrings/cursor.gpg --list-keys --keyid-format LONG | grep "42A1772E62E492D6"

# Test apt update (should show no errors)
sudo apt-get update 2>&1 | grep -i cursor
```

Expected output should show:
- Key file size: `1.2K` (not `0`)
- Key ID: `42A1772E62E492D6`
- apt update: `Get` or `Hit` (not `Err`)

## Why This Works

- The official GPG key URL (`https://downloads.cursor.com/aptrepo/gpg.key`) returns 403 Forbidden
- The alternate URL (`https://downloads.cursor.com/keys/anysphere.asc`) works and contains the correct key
- The key ID `42A1772E62E492D6` matches the repository signing key
- Using `signed-by` in the repository config ensures apt uses the correct keyring

## Notes

- This is a workaround until Cursor fixes their key distribution
- The key is from "Anysphere Inc" (Cursor's parent company)
- The fix persists across reboots
- You may need to reapply this after Cursor updates if they change their repository setup

## Related Files

- Repository config: `/etc/apt/sources.list.d/cursor.list`
- Keyring file: `/usr/share/keyrings/cursor.gpg`
- Detailed issue report: `CURSOR_GPG_KEY_ISSUE.md`
- Support email template: `CURSOR_SUPPORT_EMAIL.txt`

---
**Last Updated**: January 16, 2026
**Tested On**: Linux Mint (Ubuntu-based)

