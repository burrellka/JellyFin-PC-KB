(() => {
  const html = `
    <div>
      <div style="margin: 1rem 0;">
        <button id="pg-refresh" class="raised button-submit">Refresh Users</button>
      </div>
      <div>
        <label>Approver usernames (comma-separated)</label>
        <input id="pg-approvers" type="text" style="width:100%" placeholder="Kevin,Kady" />
      </div>
      <div style="margin-top:1rem;">
        <label>Child usernames (comma-separated)</label>
        <input id="pg-children" type="text" style="width:100%" placeholder="KJ,Kyle,Kids" />
      </div>
      <div style="margin-top:1rem;">
        <label>Daily budget (minutes)</label>
        <input id="pg-budget" type="number" value="240" />
      </div>
      <div style="margin-top:1rem;">
        <label>Allowed hours (HH:MM-HH:MM)</label>
        <input id="pg-hours" type="text" value="07:00-19:00" />
      </div>
      <div style="margin-top:1rem;">
        <button id="pg-apply" class="raised button-submit">Apply Defaults</button>
      </div>
      <div id="pg-status" style="margin-top:1rem;"></div>
    </div>`;

  const root = document.getElementById('pg-root');
  if (root) root.innerHTML = html;

  function api(path, options = {}) {
    const base = window.ApiClient.serverAddress();
    const token = window.ApiClient._authToken();
    const url = base + path;
    options.headers = Object.assign({ 'X-Emby-Token': token, 'Content-Type': 'application/json' }, options.headers || {});
    return fetch(url, options).then(r => r.json());
  }

  document.getElementById('pg-apply')?.addEventListener('click', async () => {
    const admins = (document.getElementById('pg-approvers').value || '').split(',').map(s => s.trim()).filter(Boolean);
    const profiles = (document.getElementById('pg-children').value || '').split(',').map(s => s.trim()).filter(Boolean);
    try {
      await api('/ParentGuard/policy/seed', { method: 'POST', body: JSON.stringify({ admins, profiles }) });
      document.getElementById('pg-status').textContent = 'Applied default policies.';
    } catch (e) {
      document.getElementById('pg-status').textContent = 'Error applying defaults.';
    }
  });
})();


