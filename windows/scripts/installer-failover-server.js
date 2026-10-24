// Local-only fixture server. It exposes one public FFmpeg archive, never a directory.
const http = require('node:http');
const fs = require('node:fs');
const archive = process.argv[2];
const stateFile = process.argv[3];
const requests = [];
const server = http.createServer((req, res) => {
  requests.push(req.url);
  fs.writeFileSync(stateFile + '.requests.json', JSON.stringify(requests));
  if (req.url === '/component.zip') {
    res.writeHead(200, {'Content-Type':'application/zip','Content-Length':fs.statSync(archive).size});
    fs.createReadStream(archive).pipe(res);
  } else if (req.url === '/corrupt.zip') {
    res.writeHead(200, {'Content-Type':'application/zip'}); res.end('corrupt component fixture');
  } else if (req.url === '/timeout.zip') {
    res.writeHead(504); res.end('Gateway Timeout fixture');
  } else {
    res.writeHead(404); res.end('Missing fixture');
  }
});
server.listen(0, '127.0.0.1', () => {
  fs.writeFileSync(stateFile, JSON.stringify({port:server.address().port,pid:process.pid}));
});
