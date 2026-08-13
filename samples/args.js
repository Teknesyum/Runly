#!/usr/bin/env node

const args = process.argv.slice(2);
console.log("Aldığım argümanlar:");
args.forEach((arg, index) => {
    console.log(`  ${index + 1}. ${arg}`);
});

if (args.length === 0) {
    console.log("  (hiç argüman yok)");
}

process.exit(0);
