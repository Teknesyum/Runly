const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const lum = h => { const [r, g, b] = hex(h).map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const tint = (base, c, a) => '#' + hex(base).map((v, i) => Math.trunc(v + (hex(c)[i] - v) * a / 255)).map(v => v.toString(16).padStart(2, '0')).join('').toLocaleUpperCase('tr');
const bgr = u => '#' + [u & 0xff, (u >> 8) & 0xff, (u >> 16) & 0xff].map(v => v.toString(16).padStart(2, '0')).join('').toLocaleUpperCase('tr');
const BG = bgr(0x000000), SURF = bgr(0x0A0908);
const S = '#08090A', BLUE = '#00F3FF', PINK = '#FF00EA', PKT = '#FF54EB', W = '#FFFFFF', OK = '#34D399';
const rows = [
  ['Başlatıcı', 'NeonWindowChrome.cs:192', 'birincil düğme yazısı (dinlenik/odak)', BG, bgr(0xFFF300), 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:192', 'ikincil düğme yazısı (dinlenik/odak)', bgr(0xFF7EC6), SURF, 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:210', 'başlık yazısı (önce, Surface zemin)', bgr(0xFFF300), SURF, 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:210', 'başlık yazısı (sonra, Bg zemin)', bgr(0xFFF300), BG, 7],
  ['Başlatıcı', 'NeonWindowChrome.cs:223', 'başlık simgesi − □', bgr(0xFFF300), BG, 3],
  ['Başlatıcı', 'NeonWindowChrome.cs:231', 'başlık simgesi ×', bgr(0xEA00FF), BG, 3],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:258', 'etiket (sonra, Bg zemin)', bgr(0xFFFFFF), BG, 7],
  ['Başlatıcı', 'MissingHandlerDialog.cs:214', 'etiket (sonra, Bg zemin)', bgr(0xFFFFFF), BG, 7],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:264', 'giriş yazısı (önce, EditBg #101214 token dışı)', bgr(0xFFF300), bgr(0x141210), 7],
  ['Başlatıcı', 'ArgumentPromptDialog.cs:264', 'giriş yazısı (sonra, Surface)', bgr(0xFFF300), SURF, 7],
  ['Ayarlar', 'MainForm.cs:38-39', 'Durum Bağlı (önce, yeşil yazı yeşil dolgu)', OK, tint(S, OK, 40), 7],
  ['Ayarlar', 'MainForm.cs:38-39', 'Durum Bağlı (sonra, TextBody)', W, tint(S, OK, 40), 7],
  ['Ayarlar', 'ChooseApplicationDialog.cs:456', 'seçili satır adı (önce, mavi yazı mavi dolgu)', BLUE, tint(S, BLUE, 34), 7],
  ['Ayarlar', 'ChooseApplicationDialog.cs:456', 'seçili satır adı (sonra, TextStrong)', W, tint(S, BLUE, 34), 7],
  ['Ayarlar', 'ChooseApplicationDialog.cs:503', 'Önerilen çipi (önce, pembe yazı pembe dolgu)', PKT, tint(S, PINK, 26), 7],
  ['Ayarlar', 'ChooseApplicationDialog.cs:503', 'Önerilen çipi (sonra, TextBody)', W, tint(S, PINK, 26), 7],
];
console.log('| Yüzey | Dosya:Satır | Öğe | Ön | Zemin | Oran | Eşik | Sonuç |');
console.log('|---|---|---|---|---|---|---|---|');
for (const [a, f, e, fg, bg, t] of rows) {
  const r = ratio(fg, bg);
  console.log(`| ${a} | ${f} | ${e} | ${fg} | ${bg} | ${r.toFixed(2)}:1 | ${t}:1 | ${r >= t ? 'geçti' : 'KALDI'} |`);
}
