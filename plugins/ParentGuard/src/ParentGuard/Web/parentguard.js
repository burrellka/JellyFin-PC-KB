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
      <hr/>
      <div style="margin-top:1rem;">
        <h3>Labels</h3>
        <div>
          <label>User</label>
          <input id="pg-label-user" type="text" placeholder="username" />
          <label style="margin-left:0.5rem;"><input type="checkbox" id="pg-label-approver"/> Approver</label>
          <label style="margin-left:0.5rem;"><input type="checkbox" id="pg-label-child"/> Child</label>
          <button id="pg-label-apply" class="raised button-submit" style="margin-left:0.5rem;">Apply Label</button>
        </div>
      </div>
      <div style="margin-top:1rem;">
        <h3>Approvals</h3>
        <button id="pg-requests-refresh" class="raised">Refresh Requests</button>
        <div id="pg-requests" style="margin-top:0.5rem;"></div>
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

  document.getElementById('pg-label-apply')?.addEventListener('click', async () => {
    const userId = (document.getElementById('pg-label-user').value || '').trim();
    const parentApprover = document.getElementById('pg-label-approver').checked;
    const childProfile = document.getElementById('pg-label-child').checked;
    if (!userId) return;
    try {
      await api('/ParentGuard/labels', { method: 'POST', body: JSON.stringify({ userId, parentApprover, childProfile }) });
      document.getElementById('pg-status').textContent = 'Label updated.';
    } catch (e) {
      document.getElementById('pg-status').textContent = 'Error updating label.';
    }
  });

  async function loadRequests() {
    try {
      const data = await api('/ParentGuard/requests');
      const container = document.getElementById('pg-requests');
      container.innerHTML = '';
      (data.items || []).forEach(item => {
        const div = document.createElement('div');
        div.style.margin = '0.25rem 0';
        div.textContent = `[${item.status}] user=${item.userId} reason=${item.reason}`;
        if (item.status === 'pending') {
          const approve = document.createElement('button');
          approve.textContent = 'Approve 30m';
          approve.onclick = async () => {
            await api(`/ParentGuard/requests/${item.id}/approve`, { method: 'POST', body: JSON.stringify({ durationMinutes: 30 }) });
            loadRequests();
          };
          const deny = document.createElement('button');
          deny.style.marginLeft = '0.5rem';
          deny.textContent = 'Deny';
          deny.onclick = async () => {
            await api(`/ParentGuard/requests/${item.id}/deny`, { method: 'POST' });
            loadRequests();
          };
          div.appendChild(approve);
          div.appendChild(deny);
        }
        container.appendChild(div);
      });
    } catch (e) {
      document.getElementById('pg-status').textContent = 'Error loading requests.';
    }
  }

  document.getElementById('pg-requests-refresh')?.addEventListener('click', loadRequests);
  loadRequests();
})();


