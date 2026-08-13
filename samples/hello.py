#!/usr/bin/env python3

import sys

print("Merhaba! Bu bir Python scriptidir.")
print("Aldığın argümanlar:")
for i, arg in enumerate(sys.argv[1:], 1):
    print(f"  {i}. {arg}")

if len(sys.argv) == 1:
    print("  (hiç argüman yok)")

sys.exit(0)
