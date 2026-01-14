#!/bin/bash
# Auto-restart wrapper for GutenbergSync
# Automatically restarts the sync command if it fails or is interrupted

set -e

# Default values
MAX_RETRIES=999999  # Effectively infinite
RETRY_DELAY=5       # Seconds to wait before retry
LOG_FILE=""

# Parse arguments - pass everything to gutenberg-sync
SYNC_ARGS=()
while [[ $# -gt 0 ]]; do
    case $1 in
        --max-retries)
            MAX_RETRIES="$2"
            shift 2
            ;;
        --retry-delay)
            RETRY_DELAY="$2"
            shift 2
            ;;
        --log-file)
            LOG_FILE="$2"
            shift 2
            ;;
        *)
            SYNC_ARGS+=("$1")
            shift
            ;;
    esac
done

# Function to log messages
log() {
    local message="$1"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] $message"
    if [[ -n "$LOG_FILE" ]]; then
        echo "[$timestamp] $message" >> "$LOG_FILE"
    fi
}

# Find gutenberg-sync executable
if [[ -f "./gutenberg-sync" ]]; then
    GUTENBERG_SYNC="./gutenberg-sync"
elif command -v gutenberg-sync &> /dev/null; then
    GUTENBERG_SYNC="gutenberg-sync"
elif [[ -f "./publish/gutenberg-sync" ]]; then
    GUTENBERG_SYNC="./publish/gutenberg-sync"
else
    log "ERROR: gutenberg-sync not found. Please build the project first."
    exit 1
fi

log "Starting GutenbergSync with auto-retry (max: $MAX_RETRIES retries, delay: ${RETRY_DELAY}s)"
log "Command: $GUTENBERG_SYNC sync ${SYNC_ARGS[*]}"

RETRY_COUNT=0
LAST_EXIT_CODE=0

while [[ $RETRY_COUNT -lt $MAX_RETRIES ]]; do
    if [[ $RETRY_COUNT -gt 0 ]]; then
        log "Retry attempt $RETRY_COUNT/$MAX_RETRIES (waiting ${RETRY_DELAY}s before retry...)"
        sleep "$RETRY_DELAY"
        log "Resuming sync..."
    fi

    # Run the sync command
    if "$GUTENBERG_SYNC" sync "${SYNC_ARGS[@]}"; then
        log "Sync completed successfully!"
        exit 0
    else
        LAST_EXIT_CODE=$?
        RETRY_COUNT=$((RETRY_COUNT + 1))
        
        # Check if it was a cancellation (Ctrl+C) - don't retry on manual cancellation
        if [[ $LAST_EXIT_CODE -eq 130 ]] || [[ $LAST_EXIT_CODE -eq 2 ]]; then
            log "Sync was manually cancelled (Ctrl+C). Exiting."
            exit $LAST_EXIT_CODE
        fi
        
        log "Sync failed with exit code $LAST_EXIT_CODE"
        
        if [[ $RETRY_COUNT -ge $MAX_RETRIES ]]; then
            log "Maximum retries ($MAX_RETRIES) reached. Exiting."
            exit $LAST_EXIT_CODE
        fi
    fi
done

exit $LAST_EXIT_CODE

