import socket, sys, time, os
port = int(sys.argv[1])
log = sys.argv[2]
stop = sys.argv[3]
server = socket.socket()
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind(("127.0.0.1", port))
server.listen(50)
server.settimeout(0.5)
deadline = time.time() + 1500
open(log, "a").close()
while time.time() < deadline and not os.path.exists(stop):
    try:
        client, address = server.accept()
    except socket.timeout:
        continue
    with open(log, "a") as handle:
        handle.write("connection from %s\n" % (address,))
    client.close()
