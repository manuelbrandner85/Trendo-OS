#!/usr/bin/env python3
"""Bootet das ISO in einer VM (nur virtuelles CD-ROM, keine echten Datenträger) und speichert Screenshots.
Nutzung: sudo python3 boot-qemu.py <iso> <uefi|bios> <ausgabeordner> [sekunden]"""
import json, os, shutil, socket, subprocess, sys, time
from PIL import Image
iso, mode, out = sys.argv[1:4]; secs = int(sys.argv[4]) if len(sys.argv) > 4 else 180
os.makedirs(out, exist_ok=True)
cmd = ["qemu-system-x86_64", "-m", "4096", "-smp", "2", "-cdrom", iso, "-boot", "d", "-vga", "std",
       "-display", "none", "-qmp", f"unix:{out}/qmp-{mode}.sock,server,wait=off", "-serial", f"file:{out}/serial-{mode}.log", "-nic", "user,model=virtio-net-pci"]
# KVM nur auf Wunsch (in verschachtelten VMs hängt KVM); Standard: TCG-Emulation
cmd[1:1] = ["-enable-kvm", "-cpu", "host"] if os.environ.get("TRENDO_KVM") == "1" else ["-accel", "tcg", "-cpu", "max"]
if mode == "uefi":
    vars_ = f"{out}/vars-{mode}.fd"; shutil.copy("/usr/share/OVMF/OVMF_VARS_4M.fd", vars_)
    cmd += ["-drive", "if=pflash,format=raw,readonly=on,file=/usr/share/OVMF/OVMF_CODE_4M.fd", "-drive", f"if=pflash,format=raw,file={vars_}"]
print("CMD:", " ".join(cmd), flush=True)
p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL)
time.sleep(2); q = socket.socket(socket.AF_UNIX); q.connect(f"{out}/qmp-{mode}.sock"); f = q.makefile("rw")
def qmp(c, **a):
    f.write(json.dumps({"execute": c, "arguments": a} if a else {"execute": c}) + "\n"); f.flush()
    while True:
        r = json.loads(f.readline())
        if "return" in r or "error" in r: return r
f.readline(); qmp("qmp_capabilities")
time.sleep(25); print("Bootmenü: Enter", qmp("send-key", keys=[{"type": "qcode", "data": "ret"}]), flush=True)
t0 = time.time(); shots = sorted({20, 300, secs})
for t in shots:
    time.sleep(max(0, t - (time.time() - t0)))
    ppm = f"{out}/{mode}-{t}s.ppm"; print(qmp("screendump", filename=ppm), flush=True)
    Image.open(ppm).save(ppm[:-4] + ".png"); os.remove(ppm); print("SHOT", ppm[:-4] + ".png", flush=True)
qmp("quit"); p.wait(30); os.remove(f"{out}/qmp-{mode}.sock")
if mode == "uefi": os.remove(vars_)
print("QEMU-Exit:", p.returncode)
