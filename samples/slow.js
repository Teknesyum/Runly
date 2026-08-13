#!/usr/bin/env node

console.log("3 saniye bekliyorum...");
setTimeout(() => {
    console.log("Bekleme tamamlandı!");
    process.exit(0);
}, 3000);
