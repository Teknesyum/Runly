const T = require('../../../../teknesyum-ui/theme.tokens.json');
const v = (g, k) => T[g][k].value;
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const lum = h => { const [r, g, b] = hex(h).map(x => { x /= 255; return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const over = (c, a, base) => '#' + hex(base).map((b, i) => Math.round(b + (hex(c)[i] - b) * a)).map(x => x.toString(16).padStart(2, '0')).join('').toUpperCase();
const BG = v('brand', 'black'), R1 = v('brand', 'renk-1'), R2 = v('brand', 'renk-2'), R3 = v('brand', 'renk-3');
const R2T = v('brand', 'renk-2-text'), R3T = v('brand', 'renk-3-text'), W = v('role', 'text'), OK = v('role', 'success'), WARN = v('role', 'warning'), DIS = v('role', 'disabled');
const border = T.derived.border.alpha;
const rows = [
  ['Ayarlar, Başlatıcı, Kurulum', 'gövde yazısı', W, BG, 7],
  ['Ayarlar, Başlatıcı, Kurulum', 'etiket / başlık vurgusu renk-1', R1, BG, 7],
  ['Ayarlar, Başlatıcı, Kurulum', 'birincil düğme yazısı (siyah, renk-1 dolgu)', BG, R1, 7],
  ['Ayarlar, Kurulum', 'ikincil düğme yazısı renk-3-text', R3T, BG, 7],
  ['Ayarlar, Kurulum', 'destek çipi / başlık vurgusu renk-2-text', R2T, BG, 7],
  ['Ayarlar, Kurulum', 'başarı yazısı', OK, BG, 7],
  ['Ayarlar', 'uyarı yazısı', WARN, BG, 7],
  ['Ayarlar', 'seçili satır yazısı (renk-1 %30 dolgu)', W, over(R1, 0.3, BG), 7],
  ['Ayarlar', 'hover satır yazısı (renk-1 %20 dolgu)', W, over(R1, 0.2, BG), 7],
  ['Ayarlar', 'çip yazısı (renk-2 %10 dolgu)', W, over(R2, 0.1, BG), 7],
  ['Ayarlar', 'renk-1 yazı, renk-1 %10 dolgu üstünde', R1, over(R1, 0.1, BG), 7],
  ['Ayarlar', 'edilgen yazı (WCAG muaf, bilgi için)', DIS, BG, 4.5],
  ['Ayarlar, Başlatıcı, Kurulum', 'kenarlık renk-1 /' + border, over(R1, border, BG), BG, 3],
  ['Başlatıcı', 'kapat simgesi renk-2', R2, BG, 3],
  ['Kurulum', 'ilerleme dolgusu renk-1 → renk-2', R2, BG, 3],
  ['Simge', 'belge gövdesi renk-1 / ok izi renk-2-text', R1, BG, 3],
  ['Simge', 'ok izi renk-2-text', R2T, BG, 3],
];
console.log('| Yüzey | Öğe | Ön | Zemin | Oran | Eşik | Sonuç |');
console.log('|---|---|---|---|---|---|---|');
let fail = 0;
for (const [a, e, fg, bg, t] of rows) {
  const r = ratio(fg, bg);
  if (r < t) fail++;
  console.log(`| ${a} | ${e} | ${fg} | ${bg} | ${r.toFixed(2)}:1 | ${t}:1 | ${r >= t ? 'geçti' : 'KALDI'} |`);
}
console.log(`\nKalan: ${fail}`);
