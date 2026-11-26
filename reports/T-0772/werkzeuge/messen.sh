#!/usr/bin/env bash
# T-0772 Messung alt (main 542dc1ce7) gegen neu (Zweig), abwechselnd, gleicher Harness. Ausgabe: Rohlog je Fall.
S="$1"; OUT="$2"; mkdir -p "$OUT"; cd "$S" || exit 1
run() { # run <stand> <modus> <datei> <sek> <wiederholungen>
  for i in $(seq 1 "$5"); do
    echo "### $1 $2 $3 Lauf $i $(date +%H:%M:%S)"
    timeout $(( $4 + 60 )) "./$1/h772.exe" "$2" "faelle/$3.dxf" "$4" 2>&1
  done
}
pair() { # pair <modus> <datei> <sek> <n>
  for i in $(seq 1 "$4"); do run alt "$1" "$2" "$3" 1; run neu "$1" "$2" "$3" 1; done
}
{
  echo "# Punkt 1: k5_dicht-Nachbau (Lesen + Konturen + Fräsbahnen), alt/neu abwechselnd"
  pair --phasen k5b_dicht_2000_3000_50 150 3
  run alt --phasen k5b_dicht_2000_30000_50 400 1
  run neu --phasen k5b_dicht_2000_30000_50 400 3
  run neu --phasen k5b_dicht_500_60000_50 400 1
  run neu --cancel k5b_dicht_500_60000_50 3000 1
  run alt --cancel k5b_dicht_500_60000_50 3000 1
} > "$OUT/punkt1-rohlog.txt"
{
  echo "# Punkt 2: b2 / e0 / z1, alt/neu"
  pair --phasen b2_gleicher_rahmen_versteckt_2000 170 3
  run alt --phasen b2_gleicher_rahmen_versteckt_20000 300 1
  run neu --phasen b2_gleicher_rahmen_versteckt_20000 300 1
  pair --phasen e0_kontrolle_verteilt_10000 170 3
  run alt --phasen e0_kontrolle_verteilt_100000 300 1
  run neu --phasen e0_kontrolle_verteilt_100000 300 1
  pair --phasen z1_zickzack_senkrecht_10000_versteckt_200 170 3
  run alt --phasen z1_zickzack_senkrecht_100000_versteckt_2000 300 1
  run neu --phasen z1_zickzack_senkrecht_100000_versteckt_2000 300 1
} > "$OUT/punkt2-rohlog.txt"
{
  echo "# Punkt 3: Gravur p1, alt/neu abwechselnd"
  pair --phasen p1_diag_gravur_1000_10000 170 3
  pair --phasen p1_diag_gravur_200_10000 170 3
} > "$OUT/punkt3-rohlog.txt"
echo fertig
