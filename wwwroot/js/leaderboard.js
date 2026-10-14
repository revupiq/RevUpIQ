let leaderboardRequest;

async function loadLeaderboard() {
    const host = document.getElementById('leaderboardHost');
    const dateInput = document.getElementById('leaderboardDate');
    if (!host || !dateInput || !dateInput.reportValidity()) return;

    leaderboardRequest?.abort();
    const request = new AbortController();
    leaderboardRequest = request;
    const params = new URLSearchParams({
        mode: document.querySelector('.filter-mode.selected')?.dataset.mode ?? 'All',
        time: document.querySelector('.filter-time.selected')?.dataset.time ?? 'Daily',
        divisionId: document.querySelector('.division-select')?.value ?? '0',
        date: dateInput.value
    });

    host.setAttribute('aria-busy', 'true');
    host.textContent = 'Loading leaderboard…';
    try {
        const response = await fetch(`/Leaderboard/LoadLeaderboard?${params}`, { signal: request.signal });
        if (!response.ok || response.redirected) throw new Error('Unable to load the leaderboard. Please refresh or try again.');
        const html = await response.text();
        if (leaderboardRequest === request) host.innerHTML = html;
    } catch (error) {
        if (error.name !== 'AbortError' && leaderboardRequest === request) host.textContent = error.message;
    } finally {
        if (leaderboardRequest === request) host.removeAttribute('aria-busy');
    }
}

document.addEventListener('click', function (event) {
    const button = event.target.closest('.filter-mode, .filter-time');
    if (!button) return;
    const selector = button.classList.contains('filter-mode') ? '.filter-mode' : '.filter-time';
    document.querySelectorAll(selector).forEach(item => item.classList.remove('selected'));
    button.classList.add('selected');
    loadLeaderboard();
});

document.addEventListener('change', function (event) {
    if (event.target.matches('.division-select, #leaderboardDate')) loadLeaderboard();
});

document.addEventListener('DOMContentLoaded', loadLeaderboard);
