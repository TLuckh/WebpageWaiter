import socket

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind(('localhost', 19999))
server.listen(1)

while True:
    conn, _ = server.accept()
    # Absichtlich nichts tun - Verbindung offen lassen aber nie antworten
