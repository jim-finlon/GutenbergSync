# Cursor IDE Linux APT Repository GPG Key Issue

## Problem Summary
The Cursor IDE Linux APT repository (`https://downloads.cursor.com/aptrepo`) has a persistent GPG key verification failure that prevents `apt-get update` from completing successfully on Linux systems. This issue has been reported as occurring for months, if not since the repository was first set up.

## Error Details

### Error Message
```
Err:12 https://downloads.cursor.com/aptrepo stable InRelease
  The following signatures couldn't be verified because the public key is not available: NO_PUBKEY 42A1772E62E492D6
W: GPG error: https://downloads.cursor.com/aptrepo stable InRelease: The following signatures couldn't be verified because the public key is not available: NO_PUBKEY 42A1772E62E492D6
E: The repository 'https://downloads.cursor.com/aptrepo stable InRelease' is not signed.
```

### Key ID
- **Missing Key ID**: `42A1772E62E492D6`

## Root Cause Analysis

### 1. GPG Key Download URL Returns 403 Forbidden
The documented method for obtaining the GPG key fails:
```bash
curl -fsSL https://downloads.cursor.com/aptrepo/gpg.key
# Returns: HTTP 403 Forbidden
```

**Expected Behavior**: The URL should return a valid GPG public key in ASCII-armored format.

**Actual Behavior**: The server returns HTTP 403, preventing automated installation scripts from obtaining the key.

### 2. Key Not Available on Public Keyservers
Attempts to retrieve the key from standard GPG keyservers fail:
```bash
gpg --keyserver keyserver.ubuntu.com --recv-keys 42A1772E62E492D6
# Result: "gpg: keyserver receive failed: No data"
```

**Expected Behavior**: The key should be available on public keyservers for easy retrieval.

**Actual Behavior**: The key is not found on any tested keyserver (keyserver.ubuntu.com, hkp://keyserver.ubuntu.com:80).

### 3. Repository Files Are Properly Signed
The repository's `InRelease` file is properly signed with a PGP signature, indicating the signing key exists and is being used correctly. The signature can be verified if the public key is available.

**Finding**: The repository infrastructure is working correctly; the issue is purely with key distribution.

## Current Workaround

Users must manually comment out the Cursor repository in `/etc/apt/sources.list.d/cursor.list` to prevent `apt-get update` failures:

```bash
# Comment out the repository line
sudo sed -i 's/^deb/#deb/' /etc/apt/sources.list.d/cursor.list
```

This prevents Cursor updates via APT but allows other package management to function normally.

## Recommended Fixes

### Option 1: Fix GPG Key Download URL (Recommended)
1. Ensure `https://downloads.cursor.com/aptrepo/gpg.key` returns HTTP 200 with the public key
2. Verify the key file is accessible without authentication
3. Test the URL from multiple locations/IPs to ensure it's not blocked

### Option 2: Publish Key to Public Keyservers
1. Upload the GPG public key (ID: `42A1772E62E492D6`) to major keyservers:
   - keyserver.ubuntu.com
   - pgp.mit.edu
   - keys.openpgp.org
2. Verify the key is retrievable via standard GPG commands

### Option 3: Provide Alternative Installation Method
1. Document manual key installation steps in Cursor's installation documentation
2. Provide a direct download link that works (not behind authentication)
3. Include key fingerprint verification instructions

### Option 4: Use Signed-by in Repository Configuration
Update the repository configuration to use a keyring file approach:
```bash
# Download key to keyring (if URL worked)
curl -fsSL https://downloads.cursor.com/aptrepo/gpg.key | gpg --dearmor -o /usr/share/keyrings/cursor.gpg

# Update repository config to use keyring
deb [arch=amd64,arm64 signed-by=/usr/share/keyrings/cursor.gpg] https://downloads.cursor.com/aptrepo stable main
```

## System Information
- **OS**: Linux (Ubuntu/Debian-based)
- **Issue Duration**: Months to potentially since repository creation
- **Impact**: Prevents `apt-get update` from completing successfully
- **Workaround Available**: Yes (comment out repository)

## Verification Steps

To verify the issue on a system:

1. Check repository configuration:
   ```bash
   cat /etc/apt/sources.list.d/cursor.list
   ```

2. Attempt to update package lists:
   ```bash
   sudo apt-get update 2>&1 | grep -i cursor
   ```

3. Check for keyring files:
   ```bash
   ls -la /usr/share/keyrings/ | grep cursor
   ```

4. Verify key file size (should not be 0 bytes):
   ```bash
   ls -lh /usr/share/keyrings/cursor.gpg
   ```

## Additional Notes

- The Cursor installer/updater may be creating empty keyring files (0 bytes), which suggests the key download is failing during installation
- The repository itself is functional and properly signed; only key distribution is broken
- This affects all Linux users who install Cursor via the APT repository method

## Contact Information for Reporting

This issue should be reported to:
- Cursor IDE Support/Development Team
- GitHub Issues (if Cursor has a public repository)
- Cursor Community Forums

---

**Report Generated**: January 16, 2026
**System**: Linux Mint (Ubuntu-based)
**Cursor Repository**: https://downloads.cursor.com/aptrepo

