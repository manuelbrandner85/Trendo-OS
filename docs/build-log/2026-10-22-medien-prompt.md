# 2026-10-22 Medien-Auftrag

`medien.umwandeln` kopiert die Projektdatei und lässt ffmpeg daraus eine neue Datei machen. Ein PNG wird als JPEG wieder gelesen. Ohne ffmpeg entsteht keine Datei. `medien.hochladen` lädt nichts hoch.

Bild: `TRENDO_MEDIA_PROMPT=1 dotnet run --project tools/Trendo.Render -c Release -- medien-vorschau.png 1536 1024 kreativ-medien`
