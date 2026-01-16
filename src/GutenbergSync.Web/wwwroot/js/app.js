// SignalR connection
let connection = null;

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initializeSignalR();
    setupTabs();
    setupForms();
    loadCatalogStats();
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/sync")
        .build();

    connection.on("ProgressUpdate", (progress) => {
        updateSyncProgress(progress);
    });

    connection.on("SyncComplete", (result) => {
        updateSyncStatus(false);
        showMessage("Sync completed successfully", "success");
    });

    connection.on("SyncError", (error) => {
        updateSyncStatus(false);
        showMessage(`Sync error: ${error}`, "error");
    });

    connection.start().catch(err => {
        console.error("SignalR connection error:", err);
    });
}

function setupTabs() {
    const tabButtons = document.querySelectorAll('.tab-button');
    const tabContents = document.querySelectorAll('.tab-content');

    tabButtons.forEach(button => {
        button.addEventListener('click', () => {
            const targetTab = button.dataset.tab;

            // Update buttons
            tabButtons.forEach(btn => btn.classList.remove('active'));
            button.classList.add('active');

            // Update content
            tabContents.forEach(content => content.classList.remove('active'));
            document.getElementById(`${targetTab}-tab`).classList.add('active');

            // Load stats when switching to search tab
            if (targetTab === 'search') {
                loadCatalogStats();
            }
        });
    });
}

function setupForms() {
    // Start sync button
    const startSyncBtn = document.getElementById('start-sync-btn');
    if (startSyncBtn) {
        startSyncBtn.addEventListener('click', async () => {
            try {
                updateSyncStatus(true);
                showMessage("Starting sync...", "success");
                
                const response = await fetch('/api/Api/sync/start', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                });
                
                if (!response.ok) {
                    const errorData = await response.json();
                    throw new Error(errorData.error || 'Failed to start sync');
                }
                
                const data = await response.json();
                console.log("Sync started:", data);
                showMessage("Sync started successfully", "success");
            } catch (error) {
                console.error("Error starting sync:", error);
                updateSyncStatus(false);
                showMessage(`Error: ${error.message}`, "error");
            }
        });
    }

    // Search form
    document.getElementById('search-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        await performSearch();
    });

    // Copy form
    document.getElementById('copy-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        await copyEpub();
    });
}

async function loadCatalogStats() {
    try {
        const response = await fetch('/api/Api/statistics');
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Failed to load statistics');
        }
        const stats = await response.json();
        
        const statsContainer = document.getElementById('catalog-stats');
        if (!statsContainer) return;
        
        statsContainer.innerHTML = `
            <div class="stat-card">
                <div class="stat-value">${stats.totalBooks ?? stats.TotalBooks ?? 0}</div>
                <div class="stat-label">Total Books</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">${stats.totalAuthors ?? stats.TotalAuthors ?? 0}</div>
                <div class="stat-label">Authors</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">${stats.uniqueLanguages ?? stats.UniqueLanguages ?? 0}</div>
                <div class="stat-label">Languages</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">${stats.uniqueSubjects ?? stats.UniqueSubjects ?? 0}</div>
                <div class="stat-label">Subjects</div>
            </div>
        `;
    } catch (error) {
        console.error("Error loading statistics:", error);
        const statsContainer = document.getElementById('catalog-stats');
        if (statsContainer) {
            statsContainer.innerHTML = 
                `<div class="message error">Error loading catalog statistics: ${error.message}</div>`;
        }
    }
}

async function performSearch() {
    const query = document.getElementById('search-query').value;
    const author = document.getElementById('search-author').value;
    const language = document.getElementById('search-language').value;

    try {
        const response = await fetch('/api/Api/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ query, author, language, limit: 50 })
        });

        const data = await response.json();
        displaySearchResults(data.results || []);
    } catch (error) {
        showMessage(`Search error: ${error.message}`, "error");
    }
}

// Store selected books for copying
let selectedBooks = [];

function displaySearchResults(results) {
    const container = document.getElementById('search-results');
    
    if (results.length === 0) {
        container.innerHTML = '<div class="message">No results found</div>';
        return;
    }

    container.innerHTML = `
        <div style="margin-bottom: 10px;">
            <button id="select-all-btn" class="btn btn-secondary" style="margin-right: 10px;">Select All</button>
            <button id="copy-selected-btn" class="btn btn-primary" disabled>Copy Selected (0)</button>
        </div>
        ${results.map((book, index) => `
        <div class="result-item" data-book-id="${book.bookId}">
            <label style="display: flex; align-items: start; cursor: pointer;">
                <input type="checkbox" class="book-checkbox" data-book-id="${book.bookId}" 
                       style="margin-right: 10px; margin-top: 5px;" 
                       onchange="updateCopyButton()">
                <div style="flex: 1;">
                    <h3 style="margin-top: 0;">${book.title} (ID: ${book.bookId})</h3>
                    <p><strong>Authors:</strong> ${book.authors.map(a => a.name).join(', ') || 'Unknown'}</p>
                    <p><strong>Language:</strong> ${book.language || 'Unknown'}</p>
                    ${book.subjects.length > 0 ? `<p><strong>Subjects:</strong> ${book.subjects.join(', ')}</p>` : ''}
                </div>
            </label>
        </div>
    `).join('')}`;
    
    // Add event listeners
    document.getElementById('select-all-btn').addEventListener('click', () => {
        const checkboxes = document.querySelectorAll('.book-checkbox');
        const allChecked = Array.from(checkboxes).every(cb => cb.checked);
        checkboxes.forEach(cb => cb.checked = !allChecked);
        updateCopyButton();
    });
    
    document.getElementById('copy-selected-btn').addEventListener('click', () => {
        copySelectedBooks();
    });
    
    // Store results for later use
    window.searchResults = results;
}

function updateCopyButton() {
    const checkboxes = document.querySelectorAll('.book-checkbox:checked');
    const copyBtn = document.getElementById('copy-selected-btn');
    const count = checkboxes.length;
    copyBtn.disabled = count === 0;
    copyBtn.textContent = `Copy Selected (${count})`;
}

function copySelectedBooks() {
    const checkboxes = document.querySelectorAll('.book-checkbox:checked');
    const selectedIds = Array.from(checkboxes).map(cb => parseInt(cb.dataset.bookId));
    
    if (selectedIds.length === 0) {
        showMessage("No books selected", "error");
        return;
    }
    
    // Get the book data from search results
    const selectedBooksData = window.searchResults.filter(book => selectedIds.includes(book.bookId));
    
    // Switch to copy tab
    document.querySelector('[data-tab="copy"]').click();
    
    // Populate the copy form with selected books
    populateCopyTab(selectedBooksData);
}

function populateCopyTab(books) {
    const container = document.getElementById('copy-result');
    container.innerHTML = `
        <div style="margin-bottom: 15px;">
            <h3>Selected Books (${books.length})</h3>
            <ul style="list-style: none; padding: 0;">
                ${books.map(book => `
                    <li style="padding: 5px 0; border-bottom: 1px solid #ddd;">
                        <strong>${book.title}</strong> by ${book.authors.map(a => a.name).join(', ') || 'Unknown'} (ID: ${book.bookId})
                    </li>
                `).join('')}
            </ul>
        </div>
        <div>
            <label for="copy-destination">Destination Folder:</label>
            <input type="text" id="copy-destination" placeholder="/path/to/destination" required style="width: 100%; padding: 12px; border: 1px solid #ddd; border-radius: 5px; font-size: 16px; margin: 5px 0;">
            <button id="copy-all-btn" class="btn btn-primary" style="margin-top: 10px;">Copy All Selected Books</button>
        </div>
    `;
    
    document.getElementById('copy-all-btn').addEventListener('click', async () => {
        const destination = document.getElementById('copy-destination').value;
        if (!destination) {
            showMessage("Please enter a destination folder", "error", "copy-result");
            return;
        }
        
        await copyMultipleBooks(books, destination);
    });
    
    // Store books for the copy function
    window.booksToCopy = books;
}

async function copyMultipleBooks(books, destinationFolder) {
    const container = document.getElementById('copy-result');
    container.innerHTML = '<div class="message">Copying books...</div>';
    
    let successCount = 0;
    let failCount = 0;
    
    for (const book of books) {
        try {
            // Generate filename: {author name}-{Book Title}.epub
            const authorName = book.authors.length > 0 
                ? sanitizeFilename(book.authors[0].name)
                : 'Unknown';
            const title = sanitizeFilename(book.title);
            const filename = `${authorName}-${title}.epub`;
            // Use path separator appropriate for the OS (though server is Linux)
            const destinationPath = destinationFolder.endsWith('/') 
                ? destinationFolder + filename 
                : destinationFolder + '/' + filename;
            
            const response = await fetch('/api/Api/epub/copy', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    bookId: book.bookId, 
                    destinationPath: destinationPath 
                })
            });
            
            if (response.ok) {
                successCount++;
            } else {
                failCount++;
                const error = await response.json();
                console.error(`Failed to copy book ${book.bookId}:`, error);
            }
        } catch (error) {
            failCount++;
            console.error(`Error copying book ${book.bookId}:`, error);
        }
    }
    
    container.innerHTML = `
        <div class="message ${failCount === 0 ? 'success' : 'error'}">
            Copy complete: ${successCount} succeeded, ${failCount} failed
        </div>
    `;
}

async function copyEpub() {
    const bookId = parseInt(document.getElementById('copy-book-id').value);
    const destination = document.getElementById('copy-destination').value;

    try {
        const response = await fetch('/api/Api/epub/copy', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ bookId, destinationPath: destination })
        });

        const data = await response.json();
        if (response.ok) {
            showMessage("EPUB copied successfully", "success");
        } else {
            showMessage(data.error || "Failed to copy EPUB", "error");
        }
    } catch (error) {
        showMessage(`Error: ${error.message}`, "error");
    }
}

function updateSyncStatus(isRunning) {
    const dot = document.getElementById('status-dot');
    const text = document.getElementById('status-text');
    
    dot.className = 'status-dot';
    if (isRunning) {
        dot.classList.add('running');
        text.textContent = 'Running';
    } else {
        text.textContent = 'Not running';
    }
}

function updateSyncProgress(progress) {
    const container = document.getElementById('sync-progress');
    if (!container) return;
    
    let html = '';
    
    // Handle SyncOrchestrationProgress format (from SignalR)
    const phase = progress.phase || progress.Phase || 'Processing';
    const percent = progress.progressPercent !== undefined && progress.progressPercent !== null 
        ? progress.progressPercent 
        : (progress.ProgressPercent !== undefined && progress.ProgressPercent !== null 
            ? progress.ProgressPercent 
            : 0);
    const message = progress.message || progress.Message || '';
    const currentFile = progress.currentFile || progress.CurrentFile || '';
    
    html += `<div class="progress-item">
        <strong>${phase}</strong>
        ${message ? `<p>${message}</p>` : ''}
        <div class="progress-bar">
            <div class="progress-fill" style="width: ${Math.min(100, Math.max(0, percent))}%">
                ${percent.toFixed(1)}%
            </div>
        </div>
        ${currentFile ? `<p>Current: ${currentFile}</p>` : ''}
    </div>`;
    
    container.innerHTML = html;
}

function sanitizeFilename(filename) {
    // Remove invalid filename characters
    return filename.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_').trim();
}

function showMessage(message, type, containerId = 'sync-progress') {
    const container = document.getElementById(containerId);
    if (!container) return;
    
    const msgDiv = document.createElement('div');
    msgDiv.className = `message ${type}`;
    msgDiv.textContent = message;
    container.appendChild(msgDiv);
    
    setTimeout(() => msgDiv.remove(), 5000);
}

