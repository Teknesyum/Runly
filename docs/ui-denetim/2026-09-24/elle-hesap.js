const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const lum = h => { const [r, g, b] = hex(h).map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const tint = (base, c, a) => '#' + hex(base).map((v, i) => Math.trunc(v + (hex(c)[i] - v) * a / 255)).map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase();
const bgr = u => '#' + [u & 0xff, (u >> 8) & 0xff, (u >> 16) & 0xff].map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase();
const S = '#08090A', BLUE = '#00F3FF', PINK = '#FF00EA', PURPLE = '#B026FF', PT = '#C67EFF', PKT = '#FF54EB', W = '#FFFFFF', WARN = '#FBBF24', OK = '#34D399';
const rows = [
  ['Başlatıcı', 'NeonWindowChrome.cs:190', 'ikincil düğme yazısı (önce)', bgr(0xFF26B0), bgr(0x0A0908), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:192', 'ikincil düğme yazısı (sonra, PurpleText)', bgr(0xFF7EC6), bgr(0x0A0908), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:190', 'birincil düğme yazısı (önce, Surface)', bgr(0x0A0908), bgr(0xFFF300), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:192', 'birincil düğme yazısı (sonra, on-blue siyah)', bgr(0x000000), bgr(0xFFF300), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:210', 'başlık yazısı', bgr(0xFFF300), bgr(0x0A0908), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:223', 'başlık simgesi − □', bgr(0xFFF300), bgr(0x0A0908), 3],
  ['Başlatıcı', 'NeonWindowChrome.cs:231', 'başlık simgesi ×', bgr(0xEA00FF), bgr(0x0A0908), 3],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:258', 'etiket (önce, TextDim #9CA3AF token dışı)', bgr(0xAFA39C), bgr(0x0A0908), 7],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:258', 'etiket (sonra, Text)', bgr(0xFFFFFF), bgr(0x0A0908), 7],
  ['Başlatıcı', 'MissingHandlerDialog.cs:214', 'etiket (önce, TextDim)', bgr(0xAFA39C), bgr(0x0A0908), 7],
  ['Başlatıcı', 'MissingHandlerDialog.cs:214', 'etiket (sonra, Text)', bgr(0xFFFFFF), bgr(0x0A0908), 7],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:264', 'giriş yazısı (EditBg #101214 token dışı)', bgr(0xFFF300), bgr(0x141210), 7],
  ['Ayarlar', 'NeonControls.cs:381', 'ikincil düğme hover (önce, mor %12)', PT, tint(S, PURPLE, 30), 7],
  ['Ayarlar', 'NeonControls.cs:388', 'ikincil düğme hover (sonra, dolgusuz)', PT, S, 7],
  ['Ayarlar', 'NeonControls.cs:614', 'açılır liste seçili öğe (önce, mavi/mavi)', BLUE, '#00494D', 7],
  ['Ayarlar', 'NeonControls.cs:614', 'açılır liste seçili öğe (sonra, TextBody)', W, '#00494D', 7],
  ['Ayarlar', 'NeonMessageBox.cs:199', 'ileti simgesi Hata (önce, pembe hale)', PKT, tint(S, PKT, 32), 3],
  ['Ayarlar', 'NeonMessageBox.cs:198', 'ileti simgesi Hata (sonra, halesiz)', PKT, S, 3],
  ['Ayarlar', 'NeonMessageBox.cs:199', 'ileti simgesi Soru (önce, mor hale)', PT, tint(S, PT, 32), 3],
  ['Ayarlar', 'NeonGridCells.cs:356', 'Varsayılan yap hapı (önce, Warning dolgu %10)', WARN, tint(S, WARN, 26), 7],
  ['Ayarlar', 'NeonGridCells.cs:357', 'Varsayılan yap hapı (sonra, dolgusuz)', WARN, S, 7],
  ['Ayarlar', 'NeonGridCells.cs:357', 'Varsayılan yap hapı seçili satır (sonra, TextBody)', W, '#00494D', 7],
  ['Açık', 'MainForm.cs:1148', 'Durum: Bağlı (yeşil yazı yeşil dolgu, aile ihlali)', OK, tint(S, OK, 40), 7],
  ['Açık', 'ChooseApplicationDialog.cs:434', 'seçili satır adı (mavi yazı mavi dolgu)', BLUE, tint(S, BLUE, 34), 7],
  ['Açık', 'ChooseApplicationDialog.cs:479', 'Önerilen çipi (pembe yazı pembe dolgu)', PKT, tint(S, PINK, 26), 7],
];
console.log('| Yüzey | Dosya:Satır | Öğe | Ön | Zemin | Oran | Eşik | Sonuç |');
console.log('|---|---|---|---|---|---|---|---|');
for (const [a, f, e, fg, bg, t] of rows) {
  const r = ratio(fg, bg);
  console.log(`| ${a} | ${f} | ${e} | ${fg} | ${bg} | ${r.toFixed(2)}:1 | ${t}:1 | ${r >= t ? 'geçti' : 'KALDI'} |`);
}
