const api = {
  async req(method, path, body) {
    const opts = { method, headers: {} };
    if (body !== undefined) {
      opts.headers['Content-Type'] = 'application/json';
      opts.body = JSON.stringify(body);
    }
    const res = await fetch(path, opts);
    let data = null;
    try { data = await res.json(); } catch { /* no body */ }
    if (!res.ok) {
      const err = new Error((data && data.error) || `HTTP ${res.status}`);
      err.data = data;
      throw err;
    }
    return data;
  },
  get: (p) => api.req('GET', p),
  post: (p, b) => api.req('POST', p, b ?? {}),
  put: (p, b) => api.req('PUT', p, b ?? {}),
  patch: (p, b) => api.req('PATCH', p, b ?? {}),
  del: (p) => api.req('DELETE', p),
};

function val(id) { return document.getElementById(id).value; }

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function showMsg(containerId, text, type) {
  const el = document.getElementById(containerId);
  el.innerHTML = `<div class="msg ${type || 'info'}">${escapeHtml(text)}</div>`;
  setTimeout(() => { el.innerHTML = ''; }, 6000);
}

// ---- Tabs ----
document.querySelectorAll('nav button[data-tab]').forEach(btn => {
  btn.addEventListener('click', () => switchTab(btn.dataset.tab));
});

function switchTab(name) {
  document.querySelectorAll('nav button[data-tab]').forEach(b => b.classList.toggle('active', b.dataset.tab === name));
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.id === `tab-${name}`));
  if (name === 'dashboard') loadStats();
  if (name === 'jobs') loadJobs();
  if (name === 'history') loadHistory();
  if (name === 'profile') loadProfile();
  if (name === 'blacklist') loadBlacklist();
}

// ---- Dashboard ----
async function loadStats() {
  const s = await api.get('/api/stats');
  document.getElementById('stats-bar').innerHTML =
    statCard(s.jobsFoundToday, 'Bugün Bulunan') +
    statCard(s.suitableCount, 'Uygun İlan (≥70)') +
    statCard(s.pendingApprovals, 'Bekleyen Onay') +
    statCard(s.sentCount, 'Gönderilen') +
    statCard(s.failedCount, 'Başarısız') +
    statCard(s.sentToday, 'Bugün Gönderilen');

  document.getElementById('llm-warning').innerHTML = s.llmConfigured ? '' :
    `<div class="msg warn">ANTHROPIC_API_KEY tanımlı değil — sistem şu an sadece anahtar kelime tabanlı (deterministik) skorlama ve şablon e-posta kullanıyor. .env dosyasına key ekleyip uygulamayı yeniden başlat.</div>`;
}
function statCard(num, label) {
  return `<div class="stat-card"><div class="num">${num}</div><div class="label">${label}</div></div>`;
}

// ---- Intake ----
document.getElementById('intake-submit').addEventListener('click', async () => {
  const raw = val('intake-raw').trim();
  if (!raw) return;
  try {
    await api.post('/api/jobs/intake', { rawText: raw });
    document.getElementById('intake-raw').value = '';
    showMsg('intake-msg', 'İlan eklendi ve skorlandı. "İlanlar" sekmesinden görebilirsin.', 'info');
  } catch (e) {
    showMsg('intake-msg', e.message, 'error');
  }
});

document.getElementById('manual-submit').addEventListener('click', async () => {
  const body = {
    title: val('m-title'), companyName: val('m-company'), description: val('m-desc'),
    location: val('m-location') || null, workMode: val('m-workmode'),
    applyUrl: val('m-url') || null, contactEmail: val('m-email') || null
  };
  try {
    await api.post('/api/jobs/manual', body);
    ['m-title', 'm-company', 'm-desc', 'm-location', 'm-url', 'm-email'].forEach(id => document.getElementById(id).value = '');
    showMsg('intake-msg', 'İlan manuel olarak eklendi.', 'info');
  } catch (e) {
    showMsg('intake-msg', e.message, 'error');
  }
});

// ---- Jobs list + detail ----
async function loadJobs() {
  const jobs = await api.get('/api/jobs');
  document.getElementById('jobs-tbody').innerHTML = jobs.map(j => `
    <tr class="job-row" data-id="${j.id}">
      <td>${scoreBadge(j.score)}</td>
      <td>${escapeHtml(j.title)}</td>
      <td>${escapeHtml(j.companyName)}</td>
      <td>${escapeHtml(j.location ?? '-')}</td>
      <td><span class="badge status">${j.status}</span></td>
      <td>${j.applicationStatus ? `<span class="badge status">${j.applicationStatus}</span>` : '-'}</td>
    </tr>
  `).join('');
  document.querySelectorAll('#jobs-tbody tr.job-row').forEach(tr => {
    tr.addEventListener('click', () => openJobDetail(parseInt(tr.dataset.id, 10)));
  });
}

function scoreBadge(score) {
  if (score === null || score === undefined) return '<span class="badge status">—</span>';
  const cls = score >= 70 ? 'score-high' : score >= 40 ? 'score-mid' : 'score-low';
  return `<span class="badge ${cls}">%${score}</span>`;
}

async function openJobDetail(id) {
  const j = await api.get(`/api/jobs/${id}`);
  const breakdownHtml = (j.breakdown || []).map(b => `
    <div class="breakdown-item ${b.met ? 'met' : 'unmet'}">
      <span class="mark">${b.met ? '✓' : '✗'}</span>
      <span>${escapeHtml(b.requirement)} — ${escapeHtml(b.note)}</span>
    </div>
  `).join('') || '<p style="color:var(--muted)">Skor gerekçesi yok.</p>';

  const appHtml = j.application ? renderApplication(j.application) :
    `<button class="btn" onclick="generateEmail(${j.id})">Taslak E-posta Üret</button>`;

  document.getElementById('job-detail').innerHTML = `
    <div class="panel">
      <button class="close-btn" onclick="document.getElementById('job-detail').innerHTML=''">×</button>
      <h2 style="margin-top:0">${escapeHtml(j.title)} — ${escapeHtml(j.companyName)} ${scoreBadge(j.score)}</h2>
      <p style="color:var(--muted); font-size:13px;">${escapeHtml(j.location ?? '')} · ${j.workMode} · ${j.aiAssisted === true ? 'AI destekli skor' : j.aiAssisted === false ? 'Anahtar kelime tabanlı skor (AI key yok)' : ''}</p>
      <div style="white-space:pre-wrap; font-size:13px; max-height:200px; overflow:auto; background:var(--panel-2); padding:10px; border-radius:6px;">${escapeHtml(j.description)}</div>

      <h2>Uygunluk Gerekçesi</h2>
      ${breakdownHtml}

      <h2>İletişim</h2>
      <label>İletişim e-postası</label>
      <input id="contact-email-${j.id}" value="${escapeHtml(j.contactEmail ?? '')}" placeholder="hr@sirket.com" />
      <label>Kaynak</label>
      <input id="contact-source-${j.id}" value="${escapeHtml(j.contactSource ?? '')}" placeholder="örn. Kariyer sayfasından bulundu" />
      <button class="btn secondary" onclick="saveContact(${j.id})">İletişimi Kaydet</button>

      <h2>Başvuru</h2>
      ${appHtml}

      <h2>İşlemler</h2>
      <button class="btn secondary" onclick="rescoreJob(${j.id})">Yeniden Skorla</button>
      <button class="btn danger" onclick="rejectJob(${j.id})">Reddet</button>
      <button class="btn danger" onclick="deleteJob(${j.id})">Sil</button>
    </div>
  `;
}

function renderApplication(a) {
  const canEdit = a.status !== 'Sent';
  const factCheckHtml = a.factCheckIssues
    ? `<div class="msg warn">Fact-check uyarısı: ${escapeHtml(a.factCheckIssues)} — göndermeden önce mutlaka kontrol et.</div>`
    : '';
  return `
    <div class="msg info">Durum: <b>${a.status}</b> ${a.aiAssisted ? '(AI ile üretildi)' : '(şablon — AI key yok)'}</div>
    ${factCheckHtml}
    <label>Konu</label>
    <input id="app-subject-${a.id}" value="${escapeHtml(a.subject)}" ${canEdit ? '' : 'disabled'} />
    <label>İçerik</label>
    <textarea id="app-body-${a.id}" style="min-height:220px" ${canEdit ? '' : 'disabled'}>${escapeHtml(a.body)}</textarea>
    ${canEdit ? `<button class="btn secondary" onclick="saveApplicationEdit(${a.id}, ${a.jobId})">Düzenlemeyi Kaydet</button>` : ''}
    ${(a.status === 'Draft' || a.status === 'NeedsReview') ? `<button class="btn good" onclick="approveApplication(${a.id}, ${a.jobId})">Onayla</button>` : ''}
    ${a.status === 'Approved' ? `<button class="btn good" onclick="sendApplication(${a.id}, ${a.jobId})">Gönder</button>` : ''}
    ${(a.status !== 'Sent' && a.status !== 'Rejected') ? `<button class="btn danger" onclick="rejectApplication(${a.id}, ${a.jobId})">Reddet</button>` : ''}
    ${a.errorMessage ? `<div class="msg error">Hata: ${escapeHtml(a.errorMessage)}</div>` : ''}
  `;
}

async function generateEmail(jobId) {
  try { await api.post(`/api/jobs/${jobId}/generate-email`); openJobDetail(jobId); }
  catch (e) { alert(e.message); }
}
async function saveContact(jobId) {
  const email = document.getElementById(`contact-email-${jobId}`).value || null;
  const source = document.getElementById(`contact-source-${jobId}`).value || null;
  await api.patch(`/api/jobs/${jobId}/contact`, { contactEmail: email, contactSource: source });
  openJobDetail(jobId);
}
async function rescoreJob(jobId) { await api.post(`/api/jobs/${jobId}/rescore`); openJobDetail(jobId); }
async function rejectJob(jobId) { await api.post(`/api/jobs/${jobId}/reject`); document.getElementById('job-detail').innerHTML = ''; loadJobs(); }
async function deleteJob(jobId) {
  if (!confirm('Bu ilanı silmek istediğine emin misin?')) return;
  await api.del(`/api/jobs/${jobId}`);
  document.getElementById('job-detail').innerHTML = '';
  loadJobs();
}
async function saveApplicationEdit(appId, jobId) {
  const subject = document.getElementById(`app-subject-${appId}`).value;
  const body = document.getElementById(`app-body-${appId}`).value;
  try { await api.put(`/api/applications/${appId}`, { subject, body }); openJobDetail(jobId); }
  catch (e) { alert(e.message); }
}
async function approveApplication(appId, jobId) {
  try { await api.post(`/api/applications/${appId}/approve`); openJobDetail(jobId); }
  catch (e) { alert(e.message); }
}
async function rejectApplication(appId, jobId) {
  await api.post(`/api/applications/${appId}/reject`); openJobDetail(jobId);
}
async function sendApplication(appId, jobId) {
  if (!confirm('Bu başvuru gerçekten gönderilsin mi? Bu işlem geri alınamaz.')) return;
  try { await api.post(`/api/applications/${appId}/send`); openJobDetail(jobId); loadStats(); }
  catch (e) { alert(e.message); openJobDetail(jobId); }
}

// ---- History ----
async function loadHistory() {
  const items = await api.get('/api/applications');
  document.getElementById('history-tbody').innerHTML = items.map(i => `
    <tr>
      <td>${escapeHtml(i.jobTitle)}</td>
      <td>${escapeHtml(i.companyName)}</td>
      <td><span class="badge status">${i.status}</span></td>
      <td>${i.sentAt ? new Date(i.sentAt).toLocaleString('tr-TR') : '-'}</td>
      <td style="color:var(--bad)">${escapeHtml(i.errorMessage ?? '')}</td>
    </tr>
  `).join('') || '';
}

// ---- Profile ----
async function loadProfile() {
  const p = await api.get('/api/profile');
  document.getElementById('p-fullname').value = p.fullName ?? '';
  document.getElementById('p-email').value = p.email ?? '';
  document.getElementById('p-phone').value = p.phone ?? '';
  document.getElementById('p-summary').value = p.summary ?? '';
  document.getElementById('p-years').value = p.yearsOfExperience ?? 0;
  document.getElementById('p-linkedin').value = p.linkedInUrl ?? '';
  document.getElementById('p-github').value = p.githubUrl ?? '';
  document.getElementById('p-portfolio').value = p.portfolioUrl ?? '';

  document.getElementById('skills-list').innerHTML = p.skills.map(s => `
    <div class="list-item">
      <div>${escapeHtml(s.name)} <span class="meta">${escapeHtml(s.category)} · ${s.proficiency}</span></div>
      <button class="btn danger" style="margin:0" onclick="deleteSkill(${s.id})">Sil</button>
    </div>
  `).join('') || '<p style="color:var(--muted)">Henüz beceri eklenmedi.</p>';

  document.getElementById('bullets-list').innerHTML = p.bullets.map(b => `
    <div class="list-item">
      <div>${escapeHtml(b.text)} <span class="meta">${escapeHtml(b.relatedSkills)}</span></div>
      <button class="btn danger" style="margin:0" onclick="deleteBullet(${b.id})">Sil</button>
    </div>
  `).join('') || '<p style="color:var(--muted)">Henüz deneyim cümlesi eklenmedi.</p>';

  document.getElementById('resumes-list').innerHTML = p.resumes.map(r => `
    <div class="list-item">
      <div>${escapeHtml(r.fileName)} <span class="meta">${escapeHtml(r.targetRoleType)} ${r.isDefault ? '· Varsayılan' : ''}</span></div>
      <div>
        ${!r.isDefault ? `<button class="btn secondary" style="margin:0" onclick="setDefaultResume(${r.id})">Varsayılan Yap</button>` : ''}
        <button class="btn danger" style="margin:0" onclick="deleteResume(${r.id})">Sil</button>
      </div>
    </div>
  `).join('') || '<p style="color:var(--muted)">Henüz CV yüklenmedi.</p>';
}

document.getElementById('profile-save').addEventListener('click', async () => {
  const body = {
    fullName: val('p-fullname'), email: val('p-email'), phone: val('p-phone') || null,
    summary: val('p-summary'), yearsOfExperience: parseInt(val('p-years') || '0', 10),
    linkedInUrl: val('p-linkedin') || null, githubUrl: val('p-github') || null, portfolioUrl: val('p-portfolio') || null
  };
  await api.put('/api/profile', body);
  showMsg('profile-msg', 'Profil kaydedildi.', 'info');
});

document.getElementById('skill-add').addEventListener('click', async () => {
  await api.post('/api/profile/skills', { name: val('s-name'), aliases: val('s-aliases'), category: val('s-category'), proficiency: val('s-level') });
  ['s-name', 's-aliases', 's-category'].forEach(id => document.getElementById(id).value = '');
  loadProfile();
});
async function deleteSkill(id) { await api.del(`/api/profile/skills/${id}`); loadProfile(); }

document.getElementById('bullet-add').addEventListener('click', async () => {
  await api.post('/api/profile/bullets', { text: val('b-text'), relatedSkills: val('b-skills') });
  ['b-text', 'b-skills'].forEach(id => document.getElementById(id).value = '');
  loadProfile();
});
async function deleteBullet(id) { await api.del(`/api/profile/bullets/${id}`); loadProfile(); }

document.getElementById('resume-upload').addEventListener('click', async () => {
  const fileInput = document.getElementById('r-file');
  if (!fileInput.files.length) { alert('Dosya seç.'); return; }
  const fd = new FormData();
  fd.append('file', fileInput.files[0]);
  fd.append('targetRoleType', val('r-role') || 'General');
  fd.append('isDefault', document.getElementById('r-default').checked ? 'true' : 'false');
  const res = await fetch('/api/profile/resume', { method: 'POST', body: fd });
  if (!res.ok) { alert('Yükleme başarısız.'); return; }
  fileInput.value = '';
  loadProfile();
});
async function setDefaultResume(id) { await api.post(`/api/profile/resume/${id}/set-default`); loadProfile(); }
async function deleteResume(id) { await api.del(`/api/profile/resume/${id}`); loadProfile(); }

// ---- Blacklist ----
async function loadBlacklist() {
  const items = await api.get('/api/blacklist');
  document.getElementById('blacklist-list').innerHTML = items.map(b => `
    <div class="list-item">
      <div>${b.type}: ${escapeHtml(b.value)} <span class="meta">${escapeHtml(b.reason ?? '')}</span></div>
      <button class="btn danger" style="margin:0" onclick="deleteBlacklist(${b.id})">Kaldır</button>
    </div>
  `).join('') || '<p style="color:var(--muted)">Kara liste boş.</p>';
}
document.getElementById('bl-add').addEventListener('click', async () => {
  try {
    await api.post('/api/blacklist', { type: val('bl-type'), value: val('bl-value'), reason: val('bl-reason') || null });
    document.getElementById('bl-value').value = '';
    document.getElementById('bl-reason').value = '';
    loadBlacklist();
  } catch (e) { alert(e.message); }
});
async function deleteBlacklist(id) { await api.del(`/api/blacklist/${id}`); loadBlacklist(); }

// init
loadStats();
