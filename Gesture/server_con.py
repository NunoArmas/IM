from socket import *
from select import *
import sys

SERVER_PORT=8000
PORT=8081

HOST="localhost"

# BUFSIZE = 512 * 1024
BUFSIZE = 2048
MAX_BUFSIZE = 64 * 1024

class Server:
    def __init__(self):
        self.ss = socket(AF_INET, SOCK_STREAM)  # the server socket (IP \ TCP)
        self.ss.setsockopt(SOL_SOCKET, SO_REUSEADDR, 1)
        self.ss.bind((HOST, PORT))
        self.ss.listen(10)

        self.clients = {}

        self.message=None
        self.validMsg=None
        
        
    def recv(self):
        while True:
            rlist = [self.ss]+(list(self.clients.keys()))
            socks = select(rlist, [], [])[0]

            for s in socks:
                if s in self.clients:
                    self.flushin(s)
                elif s is self.ss:
                    self.accept()

            if self.validMsg:
                self.validMsg = False
                return self.message
    

    def flushin(self, s):
        """Read a chunk of data from this client.
        Enqueue any complete requests.
        Leave incomplete requests in buffer.
        This is called whenever data is available from client socket.
        """
        data = None

        try:
            data = s.recv(1024)
        except:
            print("flushin: recv(%s)" % s)
            self.delClient(s)
        else:
            if len(data) > 0:
                self.message = data
                self.validMsg=True

    def stop(self):
        self.ss.close()


    def accept(self):
        """Accept a new connection.
        """
        
        try: 
            csock, addr = self.ss.accept()
            self.addClient(csock, addr)

            print("\n###########\nAccept client")
        except:
            print("client not accepted")
            sys.exit()
    
    
    def addClient(self, csock, addr):
        """Add a client connecting in csock."""
        if csock in self.clients:
            print("Client NOT Added: %s already exists" %
                self.clients[csock])
            return

        self.clients[csock] = addr
    
    def delClient(self, csock):
        """Delete a client connected in csock."""
        if csock not in self.clients:
            print("Client NOT deleted: %s not found" % self.clients[csock])
            return

        del self.clients[csock]

        csock.close()

