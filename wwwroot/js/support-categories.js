(function () {
  const PARTIAL_URL = '/Support/CategoriesDropdown';
  const ADD_URL = '/Support/AddCategory';
  const EDIT_URL = '/Support/EditCategory';
  const DELETE_URL = '/Support/DeleteCategory';
  const NOTICE_URL = '/Support/UpdateSupportNotice';

  function getWrap(el) {
    return el ? el.closest('#scxWrap') : null;
  }

  function getMenu(wrap) {
    return wrap ? wrap.querySelector('#scxMenu') : null;
  }

  function isOpen(menu) {
    return menu && menu.style.display === 'block';
  }

  function openMenu(menu) {
    if (menu) menu.style.display = 'block';
  }

  function closeMenu(menu) {
    if (menu) menu.style.display = 'none';
  }

  function closeAll() {
    document.querySelectorAll('#scxMenu').forEach(m => closeMenu(m));
  }

  async function reloadPartial() {
    const host = document.getElementById('support-categories-container');
    if (!host) return;

    const r = await fetch(PARTIAL_URL, { cache: 'no-store' });
    if (!r.ok) return;

    host.innerHTML = await r.text();
  }

  async function postForm(url, data) {
    const body = new URLSearchParams();
    Object.keys(data).forEach(k => body.append(k, data[k]));

    const r = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
      body: body.toString()
    });

    return r.ok;
  }

  async function postJson(url, data) {
    const r = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });

    return r.ok;
  }

  document.addEventListener('click', async (e) => {
    const saveNoticeBtn = e.target.closest('#saveSupportNoticeBtn');
    if (saveNoticeBtn) {
      e.stopPropagation();

      const input = document.getElementById('supportNoticeInput');
      if (!input) return;

      const message = (input.value || '').trim();

      const done = await postJson(NOTICE_URL, message);
      if (!done) return;

      await reloadPartial();
      return;
    }

    const btn = e.target.closest('#scxBtn');
    if (btn) {
      e.stopPropagation();
      const wrap = getWrap(btn);
      const menu = getMenu(wrap);
      if (!menu) return;

      if (isOpen(menu)) closeMenu(menu);
      else {
        closeAll();
        openMenu(menu);
      }
      return;
    }

    const pick = e.target.closest('.scx-pick');
    if (pick) {
      e.stopPropagation();

      const wrap = getWrap(pick);
      if (!wrap) return;

      const selected = wrap.querySelector('#scxSelected');
      const menu = getMenu(wrap);

      const name = (pick.dataset.name || pick.textContent || '').trim();
      if (selected) selected.textContent = name;

      closeMenu(menu);
      return;
    }

    const delBtn = e.target.closest('.scx-delete');
    if (delBtn) {
      e.stopPropagation();

      const id = delBtn.dataset.id;
      if (!id) return;

      const ok = confirm('Delete this category?');
      if (!ok) return;

      const done = await postForm(DELETE_URL, { id });
      if (!done) return;

      await reloadPartial();
      return;
    }

    const addBtn = e.target.closest('.scx-add button');
    if (addBtn) {
      e.stopPropagation();

      const container = addBtn.closest('.scx-container');
      if (!container) return;

      const input = container.querySelector('.scx-add input');
      if (!input) return;

      const name = (input.value || '').trim();
      if (!name) return;

      const done = await postForm(ADD_URL, { name });
      if (!done) return;

      input.value = '';
      await reloadPartial();
      return;
    }

    closeAll();
  });

  document.addEventListener('dblclick', async (e) => {
    const pick = e.target.closest('.scx-pick');
    if (!pick) return;

    e.stopPropagation();

    const id = pick.dataset.id;
    const current = (pick.dataset.name || pick.textContent || '').trim();
    if (!id) return;

    const next = prompt('Edit category name:', current);
    if (next == null) return;

    const name = next.trim();
    if (!name) return;

    const done = await postForm(EDIT_URL, { id, name });
    if (!done) return;

    await reloadPartial();
  });

  document.addEventListener('click', (e) => {
    const inside = e.target.closest('#scxWrap');
    if (!inside) closeAll();
  });
})();
