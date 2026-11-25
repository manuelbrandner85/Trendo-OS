;; Trendo-Erweiterung „CSV als Tabelle“ (T-0766, Lizenz CC0-1.0)
;; Läuft in der Trendo-Sandbox (WebAssembly/WASI, Wasmtime): liest eine CSV-Datei von der Standardeingabe
;; und schreibt eine Markdown-Tabelle auf die Standardausgabe. Keine Dateien, kein Netz – nur Eingabe → Ausgabe.
;; Regeln: Komma trennt Spalten, Text in "…" darf Kommas und Zeilenumbrüche enthalten ("" = ein Anführungszeichen),
;; | wird zu \|, leere Zeilen fallen weg, ein UTF-8-BOM am Anfang wird übersprungen.
;; Die erste Zeile ist die Kopfzeile; danach folgt die Trennzeile | --- | … mit so vielen Spalten wie die Kopfzeile.
(module
  (import "wasi_snapshot_preview1" "fd_read" (func $fd_read (param i32 i32 i32 i32) (result i32)))
  (import "wasi_snapshot_preview1" "fd_write" (func $fd_write (param i32 i32 i32 i32) (result i32)))
  (memory (export "memory") 2)
  ;; 0..7 iovec, 16..19 gelesene/geschriebene Bytes, ab 32 feste Texte
  (data (i32.const 32) "| ")
  (data (i32.const 34) " | ")
  (data (i32.const 37) " |\n")
  (data (i32.const 40) " --- |")
  (data (i32.const 46) "\\|")
  (data (i32.const 48) "|")
  (global $in_end (mut i32) (i32.const 65536))
  (global $out_start (mut i32) (i32.const 0))
  (global $out_end (mut i32) (i32.const 0))
  (global $open (mut i32) (i32.const 0))
  (global $cells (mut i32) (i32.const 0))
  (global $rows (mut i32) (i32.const 0))

  ;; Speicher so weit vergrößern, dass [0, end) passt – sonst Abbruch (die Sandbox setzt die Grenze)
  (func $ensure (param $end i32)
    (local $have i32)
    (local.set $have (i32.mul (memory.size) (i32.const 65536)))
    (if (i32.gt_u (local.get $end) (local.get $have))
      (then
        (if (i32.eq
              (memory.grow (i32.add (i32.shr_u (i32.sub (local.get $end) (local.get $have)) (i32.const 16)) (i32.const 1)))
              (i32.const -1))
          (then unreachable)))))

  (func $read_all
    (local $n i32)
    (loop $more
      (call $ensure (i32.add (global.get $in_end) (i32.const 65536)))
      (i32.store (i32.const 0) (global.get $in_end))
      (i32.store (i32.const 4) (i32.const 65536))
      (if (call $fd_read (i32.const 0) (i32.const 0) (i32.const 1) (i32.const 16)) (then unreachable))
      (local.set $n (i32.load (i32.const 16)))
      (global.set $in_end (i32.add (global.get $in_end) (local.get $n)))
      (br_if $more (local.get $n))))

  (func $emit (param $p i32) (param $len i32)
    (call $ensure (i32.add (global.get $out_end) (local.get $len)))
    (memory.copy (global.get $out_end) (local.get $p) (local.get $len))
    (global.set $out_end (i32.add (global.get $out_end) (local.get $len))))

  (func $emit_byte (param $b i32)
    (call $ensure (i32.add (global.get $out_end) (i32.const 1)))
    (i32.store8 (global.get $out_end) (local.get $b))
    (global.set $out_end (i32.add (global.get $out_end) (i32.const 1))))

  (func $start_row
    (if (i32.eqz (global.get $open))
      (then
        (call $emit (i32.const 32) (i32.const 2))
        (global.set $open (i32.const 1))
        (global.set $cells (i32.const 1)))))

  (func $end_row
    (local $k i32)
    (if (global.get $open)
      (then
        (call $emit (i32.const 37) (i32.const 3))
        (if (i32.eqz (global.get $rows))
          (then
            (call $emit (i32.const 48) (i32.const 1))
            (local.set $k (global.get $cells))
            (loop $sep
              (call $emit (i32.const 40) (i32.const 6))
              (local.set $k (i32.sub (local.get $k) (i32.const 1)))
              (br_if $sep (i32.gt_s (local.get $k) (i32.const 0))))
            (call $emit_byte (i32.const 10))))
        (global.set $rows (i32.add (global.get $rows) (i32.const 1)))
        (global.set $open (i32.const 0)))))

  (func $convert
    (local $i i32) (local $c i32) (local $inq i32)
    (local.set $i (i32.const 65536))
    ;; UTF-8-BOM überspringen
    (if (i32.ge_u (i32.sub (global.get $in_end) (local.get $i)) (i32.const 3))
      (then
        (if (i32.eq (i32.and (i32.load (local.get $i)) (i32.const 0xFFFFFF)) (i32.const 0xBFBBEF))
          (then (local.set $i (i32.add (local.get $i) (i32.const 3)))))))
    (block $done
      (loop $next
        (br_if $done (i32.ge_u (local.get $i) (global.get $in_end)))
        (local.set $c (i32.load8_u (local.get $i)))
        (local.set $i (i32.add (local.get $i) (i32.const 1)))
        (block $handled
          ;; \r immer überspringen
          (br_if $handled (i32.eq (local.get $c) (i32.const 13)))
          ;; Zeilenende
          (if (i32.eq (local.get $c) (i32.const 10))
            (then
              (if (local.get $inq)
                (then (call $emit_byte (i32.const 32)))
                (else (call $end_row)))
              (br $handled)))
          (call $start_row)
          ;; Anführungszeichen
          (if (i32.eq (local.get $c) (i32.const 34))
            (then
              (if (local.get $inq)
                (then
                  (if (i32.and
                        (i32.lt_u (local.get $i) (global.get $in_end))
                        (i32.eq (i32.load8_u (local.get $i)) (i32.const 34)))
                    (then
                      (call $emit_byte (i32.const 34))
                      (local.set $i (i32.add (local.get $i) (i32.const 1))))
                    (else (local.set $inq (i32.const 0)))))
                (else (local.set $inq (i32.const 1))))
              (br $handled)))
          ;; Komma außerhalb von "…" trennt Spalten
          (if (i32.and (i32.eq (local.get $c) (i32.const 44)) (i32.eqz (local.get $inq)))
            (then
              (call $emit (i32.const 34) (i32.const 3))
              (global.set $cells (i32.add (global.get $cells) (i32.const 1)))
              (br $handled)))
          ;; | würde die Tabelle zerschneiden
          (if (i32.eq (local.get $c) (i32.const 124))
            (then (call $emit (i32.const 46) (i32.const 2)) (br $handled)))
          (call $emit_byte (local.get $c)))
        (br $next)))
    (call $end_row))

  (func $write_all
    (local $p i32) (local $left i32) (local $n i32)
    (local.set $p (global.get $out_start))
    (local.set $left (i32.sub (global.get $out_end) (global.get $out_start)))
    (block $done
      (loop $more
        (br_if $done (i32.eqz (local.get $left)))
        (i32.store (i32.const 0) (local.get $p))
        (i32.store (i32.const 4) (local.get $left))
        (if (call $fd_write (i32.const 1) (i32.const 0) (i32.const 1) (i32.const 16)) (then unreachable))
        (local.set $n (i32.load (i32.const 16)))
        (if (i32.eqz (local.get $n)) (then unreachable))
        (local.set $p (i32.add (local.get $p) (local.get $n)))
        (local.set $left (i32.sub (local.get $left) (local.get $n)))
        (br $more))))

  (func (export "_start")
    (call $read_all)
    (global.set $out_start (i32.and (i32.add (global.get $in_end) (i32.const 15)) (i32.const -16)))
    (global.set $out_end (global.get $out_start))
    (call $convert)
    (call $write_all)))
