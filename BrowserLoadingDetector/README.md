Das Projekt tut folgendes bei Ausführung:
1. Warte 200ms 
2. Suche das aktuelle fokussierte Fenster
3. Gib dessen Prozess-ID an FlaUI weiter
4. FlaUI sucht nun nach dem Ladebalken-Symbol, welches in zwei Zuständen vorliegen kann:  
Als Neu-Laden-Kreis, oder aber als Stopp-das-Laden-X
5. Per Hough-Transformation wird entschieden, welcher der beiden Fälle vorliegt
6. Gibt True zurück, wenn Laden fertig ist (Neu-Laden-Kreis gefunden)
