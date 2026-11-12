/*
 * The mock-up's helpers: the icons (Lucide's, as the plugin's Icons.cs draws them), the sample
 * gear and looks, and the parts every frame repeats (Dalamud's title bar, the plugin's header
 * bar, item tiles, rings, palette strips). P hides the pins; B shows how each part is built.
 */
(function () {
  const base = document.currentScript.src.replace(/kit\.js.*$/, '')

  const P = {
    house: ['M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8', 'M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'],
    hanger: ['M12 6.5a2.2 2.2 0 1 1 2.2 2.2c-1.2 0-2.2.6-2.2 1.8v.6l-8 5.4a1 1 0 0 0 .6 1.8h14.8a1 1 0 0 0 .6-1.8l-8-5.4'],
    layers: ['m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z', 'm22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65', 'm22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65'],
    archive: ['M3 3h18a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z', 'M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8', 'M10 12h4'],
    box: ['M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z', 'm3.3 7 8.7 5 8.7-5', 'M12 22V12'],
    package: ['M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z', 'M12 22V12', 'm3.3 7 7.703 4.734a2 2 0 0 0 1.994 0L20.7 7', 'm7.5 4.27 9 5.15'],
    sliders: ['M21 4h-7M10 4H3M21 12h-9M8 12H3M21 20h-5M12 20H3M14 2v4M8 10v4M16 18v4'],
    refresh: ['M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8', 'M21 3v5h-5', 'M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16', 'M8 16H3v5'],
    'arrow-left': ['m12 19-7-7 7-7', 'M19 12H5'],
    'arrow-right': ['M5 12h14', 'm12 5 7 7-7 7'],
    ext: ['M15 3h6v6', 'M10 14 21 3', 'M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6'],
    'chev-l': ['m15 18-6-6 6-6'],
    'chev-r': ['m9 18 6-6-6-6'],
    'chev-d': ['m6 9 6 6 6-6'],
    'chev-u': ['m18 15-6-6-6 6'],
    check: ['M20 6 9 17l-5-5'],
    x: ['M18 6 6 18', 'm6 6 12 12'],
    star: ['M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z'],
    heart: ['M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z'],
    camera: ['M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z', 'M15 13a3 3 0 1 1-6 0a3 3 0 1 1 6 0'],
    search: ['m21 21-4.34-4.34', 'M19 11a8 8 0 1 1-16 0a8 8 0 1 1 16 0'],
    plus: ['M5 12h14', 'M12 5v14'],
    minus: ['M5 12h14'],
    alert: ['m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3', 'M12 9v4', 'M12 17h.01'],
    eye: ['M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0', 'M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0'],
    shirt: ['M20.38 3.46 16 2a4 4 0 0 1-8 0L3.62 3.46a2 2 0 0 0-1.34 2.23l.58 3.47a1 1 0 0 0 .99.84H6v10c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V10h2.15a1 1 0 0 0 .99-.84l.58-3.47a2 2 0 0 0-1.34-2.23z'],
    bag: ['M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z', 'M3 6h18', 'M16 10a4 4 0 0 1-8 0'],
    shield: ['M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z'],
    users: ['M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2', 'M13 7a4 4 0 1 1-8 0a4 4 0 1 1 8 0', 'M22 21v-2a4 4 0 0 0-3-3.87', 'M16 3.13a4 4 0 0 1 0 7.75'],
    user: ['M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2', 'M16 7a4 4 0 1 1-8 0a4 4 0 1 1 8 0'],
    store: ['m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7', 'M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8', 'M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4', 'M2 7h20', 'M22 7v3a2 2 0 0 1-2 2 2.7 2.7 0 0 1-2-1 2.7 2.7 0 0 1-4 0 2.7 2.7 0 0 1-4 0 2.7 2.7 0 0 1-4 0 2.7 2.7 0 0 1-2 1 2 2 0 0 1-2-2V7'],
    droplet: ['M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7z'],
    link: ['M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71', 'M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71'],
    unlink: ['m18.84 12.25 1.72-1.71h-.02a5.004 5.004 0 0 0-.12-7.07 5.006 5.006 0 0 0-6.95 0l-1.72 1.71', 'm5.17 11.75-1.71 1.71a5.004 5.004 0 0 0 .12 7.07 5.006 5.006 0 0 0 6.95 0l1.71-1.71', 'M8 2v3M2 8h3M16 19v3M19 16h3'],
    clock: ['M22 12a10 10 0 1 1-20 0a10 10 0 1 1 20 0', 'M12 6v6l4 2'],
    copy: ['M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z', 'M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2'],
    save: ['M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z', 'M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7', 'M7 3v4a1 1 0 0 0 1 1h7'],
    send: ['M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z', 'm21.854 2.147-10.94 10.939'],
    pencil: ['M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z'],
    trash: ['M3 6h18', 'M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6', 'M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2'],
    gamepad: ['M6 11h4M8 9v4M15 12h.01M18 10h.01', 'M17.32 5H6.68a4 4 0 0 0-3.978 3.59C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258A4 4 0 0 0 17.32 5z'],
    swords: ['M14.5 17.5 3 6V3h3l11.5 11.5', 'M13 19l6-6', 'M16 16l4 4', 'M19 21l2-2'],
    info: ['M22 12a10 10 0 1 1-20 0a10 10 0 1 1 20 0', 'M12 16v-4', 'M12 8h.01'],
    key: ['m15.5 7.5 2.3 2.3a1 1 0 0 0 1.4 0l2.1-2.1a1 1 0 0 0 0-1.4L19 4', 'm21 2-9.6 9.6', 'M13 15.5a5.5 5.5 0 1 1-11 0a5.5 5.5 0 1 1 11 0'],
    play: ['M6 3l14 9-14 9z'],
    stop: ['M6 6h12v12H6z'],
    list: ['M3 12h.01M3 18h.01M3 6h.01M8 12h13M8 18h13M8 6h13'],
    folder: ['M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z'],
    chat: ['M7.9 20A9 9 0 1 0 4 16.1L2 22Z'],
    flag: ['M4 22V4a1 1 0 0 1 .4-.8A6 6 0 0 1 8 2c3 0 5 2 7.333 2q2 0 3.067-.8A1 1 0 0 1 20 4v10a1 1 0 0 1-.4.8A6 6 0 0 1 16 16c-3 0-5-2-8-2a6 6 0 0 0-4 1.528'],
    wand: ['m21.64 3.64-1.28-1.28a1.21 1.21 0 0 0-1.72 0L2.36 18.64a1.21 1.21 0 0 0 0 1.72l1.28 1.28a1.2 1.2 0 0 0 1.72 0L21.64 5.36a1.2 1.2 0 0 0 0-1.72', 'm14 7 3 3', 'M5 6v4', 'M19 14v4', 'M10 2v2', 'M7 8H3', 'M21 16h-4', 'M11 3H9'],
    mic: ['M21 12a9 9 0 1 1-18 0a9 9 0 1 1 18 0', 'M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0'],
    dots: ['M13 12a1 1 0 1 1-2 0a1 1 0 1 1 2 0', 'M20 12a1 1 0 1 1-2 0a1 1 0 1 1 2 0', 'M6 12a1 1 0 1 1-2 0a1 1 0 1 1 2 0'],
    grip: ['M9 6h.01M9 12h.01M9 18h.01M15 6h.01M15 12h.01M15 18h.01'],
    ring: ['M19 14a7 7 0 1 1-14 0a7 7 0 1 1 14 0', 'M9 3h6l-3 4z'],
    image: ['M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z', 'M11 9a2 2 0 1 1-4 0a2 2 0 1 1 4 0', 'm21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21'],
    globe: ['M22 12a10 10 0 1 1-20 0a10 10 0 1 1 20 0', 'M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20', 'M2 12h20'],
  }
  const icon = (name, cls = '') =>
    `<svg class="i ${cls}" viewBox="0 0 24 24">${(P[name] || P.info).map((d) => `<path d="${d}"/>`).join('')}</svg>`

  // The sample gear (ids, names and the game's icons, from the site's sample data).
  const I = {}
  ;[
    [2925, "Weaver's Gibus", 'Head', 41143], [3234, "Weaver's Swallowtail", 'Body', 42329], [3486, "Weaver's Trousers", 'Legs', 45332],
    [3711, "Weaver's Gloves", 'Hands', 44190], [3932, "Weaver's Thighboots", 'Feet', 46350], [4199, 'Moonlet', 'Ears', 55237],
    [4303, 'Band of Eternal Passion', 'Neck', 54839], [6132, "Master Weaver's Ring", 'Ring', 54528], [6973, 'Valentione Apron Dress', 'Body', 42351],
    [6974, 'Valentione Mitts', 'Hands', 44232], [6977, 'Valentione Pattens', 'Feet', 46380], [8558, 'Crescent Moon Nightcap', 'Head', 41624],
    [8559, 'Crescent Moon Nightgown', 'Body', 42383], [8560, 'Crescent Moon Bottoms', 'Legs', 45466], [8561, 'Crescent Moon Slippers', 'Feet', 49832],
    [8672, 'Gloves of Eternal Innocence', 'Hands', 44292], [8681, 'Veil of Eternal Innocence', 'Head', 40672], [8684, 'Gown of Eternal Innocence', 'Body', 42409],
    [10012, 'Bunny Tights', 'Legs', 45533], [13099, "Witch's Hat", 'Head', 41277], [13100, "Witch's Coatee", 'Body', 42476],
    [13101, "Witch's Gloves", 'Hands', 44396], [13102, "Witch's Thighboots", 'Feet', 46547], [28558, 'Valentione Rose Hat', 'Head', 41477],
    [42631, 'Pedagogy Mortarboard', 'Head', 56655], [42632, 'Pedagogy Gown', 'Body', 57039], [42633, 'Pedagogy Halfgloves', 'Hands', 56164],
    [42634, 'Pedagogy Culottes', 'Legs', 57567], [42635, 'Pedagogy Longboots', 'Feet', 49660], [47207, 'Fuath Plumed Hat', 'Head', 56859],
    [47208, 'Fuath Swallowtail', 'Body', 57262], [47209, 'Fuath Dress Gloves', 'Hands', 56367], [47210, 'Fuath Pantaloons', 'Legs', 57783],
    [47211, 'Fuath Dress Shoes', 'Feet', 57872],
  ].forEach(([id, name, slot, ic]) => (I[id] = { id, name, slot, ic }))

  const L = {
    scholar: [42631, 42632, 42633, 42634, 42635, 4199, 4303],
    witch: [13099, 13100, 13101, 10012, 13102],
    moon: [8558, 8559, 8560, 8561],
    fuath: [47207, 47208, 47209, 47210, 47211],
    valen: [28558, 6973, 6974, 10012, 6977],
    weaver: [2925, 3234, 3711, 3486, 3932, 6132],
    wed: [8681, 8684, 8672, 4199, 4303],
  }
  const DYE = {
    'Pastel Purple': '#bbb5da', 'Midnight Blue': '#181937', 'Pure White': '#f9f8f4', 'Jet Black': '#1e1e1e', 'Metallic Purple': '#a366c2',
    'Snow White': '#e4dfd0', 'Lotus Pink': '#fecef5', 'Rose Pink': '#e69f96', 'Dalamud Red': '#781a1a', 'Pearl White': '#e9e3da',
    'Ink Blue': '#1a1f27', 'Sky Blue': '#83b0d2', 'Lavender Purple': '#877fae', 'Regal Purple': '#66304e', 'Soot Black': '#2b2923',
    'Turquoise Blue': '#04afcd', 'Gunmetal Black': '#606068', 'Wine Red': '#451511', 'Cream Yellow': '#f2d770', 'Iris Purple': '#b79ebc',
  }
  const dark = (hex) => {
    const n = parseInt(hex.slice(1), 16)
    return ((n >> 16) * 0.3 + ((n >> 8) & 255) * 0.59 + (n & 255) * 0.11) < 110
  }
  window.GS = { icon, I, L, DYE }

  const tile = (id, cls = '', own = '') => {
    const it = I[id]
    return `<b class="it ${cls}"${own ? ` data-own="${own}"` : ''}><img src="${base}icons/${it.ic}.webp" alt=""></b>`
  }
  window.GS.tile = tile

  function fill(root) {
    root.querySelectorAll('i[data-i]').forEach((el) => {
      el.outerHTML = icon(el.dataset.i, el.className)
    })
    root.querySelectorAll('.it[data-it]').forEach((el) => {
      const it = I[el.dataset.it]
      if (it && !el.querySelector('img')) el.innerHTML = `<img src="${base}icons/${it.ic}.webp" alt="">`
      if (it && !el.title) el.title = it.name
    })
    root.querySelectorAll('[data-look]').forEach((el) => {
      const ids = L[el.dataset.look] || []
      const n = +(el.dataset.n || 4)
      const cls = el.dataset.s ? 's' + el.dataset.s : ''
      const own = (el.dataset.own || '').split(',')
      el.innerHTML = ids.slice(0, n).map((id, k) => tile(id, cls, own[k] || '')).join('')
    })
    root.querySelectorAll('[data-dyes]').forEach((el) => {
      const big = el.classList.contains('big')
      el.innerHTML = el.dataset.dyes
        .split(',')
        .map((n) => {
          const c = DYE[n.trim()] || '#888'
          return `<i style="background:${c}" class="${dark(c) ? 'dk' : ''}">${big ? n.trim() : ''}</i>`
        })
        .join('')
    })
    root.querySelectorAll('[data-sw]').forEach((el) => {
      const c = DYE[el.dataset.sw]
      el.style.background = c || 'transparent'
      if (!c) el.classList.add('none')
    })
    root.querySelectorAll('[data-ring]').forEach((el) => {
      const [a, b] = el.dataset.ring.split('/').map(Number)
      const size = el.classList.contains('l') ? 120 : +(el.dataset.size || 64)
      const w = el.classList.contains('l') ? 9 : +(el.dataset.w || 6)
      const r = (size - w) / 2
      const c = 2 * Math.PI * r
      const onField = !!el.closest('.field') && !el.closest('.callout')
      const track = onField ? 'rgb(22 13 51 / 0.18)' : '#242036'
      const arc = el.dataset.c || (onField ? '#160d33' : a >= b ? '#8ce2ff' : '#8ce2ff')
      el.style.width = el.style.height = size + 'px'
      el.insertAdjacentHTML(
        'afterbegin',
        `<svg width="${size}" height="${size}"><circle cx="${size / 2}" cy="${size / 2}" r="${r}" fill="none" stroke="${track}" stroke-width="${w}"/>` +
          `<circle cx="${size / 2}" cy="${size / 2}" r="${r}" fill="none" stroke="${arc}" stroke-width="${w}" stroke-linecap="round" stroke-dasharray="${(c * a) / b} ${c}"/></svg>`,
      )
    })
  }

  // Dalamud's title bar, and the plugin's header bar under it.
  const PLACES = [
    ['home', 'house', 'Home'],
    ['looks', 'hanger', 'Looks'],
    ['plates', 'layers', 'Plates'],
    ['closet', 'box', 'Closet'],
    ['room', 'package', 'Make room'],
  ]
  function chrome(win) {
    const title = win.dataset.title || 'Glamour Studio'
    const tb = `<div class="tb"><span class="tri"></span><span class="t">${title}</span><span class="db" title="Dalamud's window menu">&#9776;</span><span class="db" title="Dalamud's close">&#10005;</span></div>`
    let bar = ''
    if (win.dataset.place) {
      const counts = (win.dataset.counts || '|32|14/20|612|612/800').split('|')
      const narrow = win.dataset.narrow !== undefined
      const places = PLACES.map(
        ([k, ic, label], i) =>
          `<a class="place${win.dataset.place === k ? ' on' : ''}"${narrow ? ` title="${label}"` : ''}>${icon(ic)}${narrow ? '' : label}${!narrow && counts[i] ? ` <span class="n">${counts[i]}</span>` : ''}</a>`,
      ).join('')
      const [tone, text] = (win.dataset.who || 'ok|Closet sent 2 min ago').split('|')
      const who = win.dataset.char === '' ? '' : win.dataset.char || 'Koko Nyan'
      const initials = who ? who.split(' ').map((w) => w[0]).slice(0, 2).join('') : ''
      const face = who ? `<span class="face${tone === 'no' || tone === 'none' ? ' off' : ''}">${initials}</span>` : `<span class="face off">?</span>`
      const pill = narrow
        ? `<span class="who ${tone}" style="padding-right:8px">${face}<span class="dot ${tone === 'ok' ? '' : tone}"></span></span>`
        : `<span class="who ${tone}" title="${who}">${face}<span class="dot ${tone === 'ok' ? '' : tone}"></span><span class="st">${text}</span></span>`
      bar =
        `<div class="bar"><span class="brand" data-build="texture: logo">${'<span class="logo"></span>'}${narrow ? '' : 'Glamour Studio'}</span>` +
        `<span class="slash">/</span>${places}<span class="end">${pill}` +
        `<span class="btn round r36 quiet" title="Read it all again">${icon('refresh')}</span>` +
        `<span class="btn round r36 quiet" title="Settings">${icon('sliders')}</span></span></div>`
    } else if (win.dataset.back) {
      const right = win.dataset.right || ''
      bar =
        `<div class="bar"><span class="btn round r36" title="${win.dataset.back}">${icon('arrow-left')}</span>` +
        `<span class="back-title">${win.dataset.eyebrow ? `<span class="eyebrow">${win.dataset.eyebrow}</span>` : ''}<b>${win.dataset.head || ''}</b></span>` +
        `<span class="end">${right}${win.dataset.noref !== undefined ? '' : `<span class="btn round r36 quiet">${icon('refresh')}</span>`}` +
        `${win.dataset.nosettings !== undefined ? '' : `<span class="btn round r36 quiet">${icon('sliders')}</span>`}</span></div>`
    }
    win.insertAdjacentHTML('afterbegin', tb + bar)
  }

  // A companion pane's header: the tag, GLAMOUR STUDIO, what it's beside; refresh, open, fold.
  function paneHead(p) {
    const right = p.dataset.side === 'left' ? 'chev-r' : 'chev-l'
    p.insertAdjacentHTML(
      'afterbegin',
      `<div class="ph"><span class="logo s22" data-build="texture: logo"></span><span class="tt"><span class="eyebrow">Glamour Studio</span><b>${p.dataset.pane}</b></span>` +
        `<span class="btn round r28 quiet" title="Read it all again">${icon('refresh', 's14')}</span>` +
        `<span class="btn round r28 quiet" title="Open Glamour Studio">${icon('ext', 's14')}</span>` +
        `<span class="btn round r28 quiet" title="Fold it away">${icon(right, 's14')}</span></div>`,
    )
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.win').forEach(chrome)
    document.querySelectorAll('.pane[data-pane]').forEach(paneHead)
    fill(document)
    const toggle = (cls, btn) => {
      document.body.classList.toggle(cls)
      if (btn) btn.classList.toggle('on', document.body.classList.contains(cls))
    }
    const bb = document.querySelector('[data-toggle="build"]')
    const pb = document.querySelector('[data-toggle="pins"]')
    if (bb) bb.addEventListener('click', () => toggle('show-build', bb))
    if (pb) pb.addEventListener('click', () => toggle('no-pins', pb))
    document.addEventListener('keydown', (e) => {
      if (e.target.closest('input,textarea')) return
      if (e.key === 'p' || e.key === 'P') toggle('no-pins', pb)
      if (e.key === 'b' || e.key === 'B') toggle('show-build', bb)
    })
  })
})()
