const functions = require('firebase-functions');
let express = require('express')
let request = require('request')

const CLIENT_ID = functions.config().spotify.id
const CLIENT_SECRET = functions.config().spotify.key
const REDIRECT_URI = functions.config().spotify.redirect_uri
const FRONTEND_URI = functions.config().spotify.frontend_uri

let app = express()
app.use(express.json())

let redirect_uri = REDIRECT_URI
  
app.get('/success', function(req, res) {
  var html = `<!DOCTYPE html>
  <html lang="en">
      <head>
      <!-- Global site tag (gtag.js) - Google Analytics -->
      <script async src="https://www.googletagmanager.com/gtag/js?id=G-6RM7T63SXR"></script>
      <script>
        window.dataLayer = window.dataLayer || [];
        function gtag(){dataLayer.push(arguments);}
        gtag('js', new Date());

        gtag('config', 'G-6RM7T63SXR');
      </script>
          <link rel="stylesheet" href="styles.css">
      </head>
      <body>
        <div class="alert">
              <h2>Login Complete. Return to GTA 5.</h2>
              <h3>You may close this tab.</h3>
          </div>
          <div class="container center">
              <h1>GTA 5 Spotify Radio Mod</h1>
          </div>
      </body>
  </html>`;
  res.writeHead(200, {"Content-Type": "text/HTML"});
  res.write(html);
  res.end();
});

app.get('/login', function(req, res) {
  let querystring = require('querystring')
  res.redirect('https://accounts.spotify.com/authorize?' +
    querystring.stringify({
      response_type: 'code',
      client_id: CLIENT_ID,
      scope: 'user-read-private user-read-email user-modify-playback-state user-read-currently-playing user-read-playback-state user-library-read',
      redirect_uri
    }))
})

app.get('/callback', function(req, res) {
  let code = req.query.code || null
  let authOptions = {
    url: 'https://accounts.spotify.com/api/token',
    form: {
      code: code,
      redirect_uri,
      grant_type: 'authorization_code'
    },
    headers: {
      'Authorization': 'Basic ' + (new Buffer.from(
        CLIENT_ID+ ':' + CLIENT_SECRET
      ).toString('base64'))
    },
    json: true
  }

  request.post(authOptions, function(error, response, body) {
    var access_token = body.access_token
    let uri = FRONTEND_URI
    res.redirect(uri + '/success?access_token=' + access_token)
  })
})

exports.app = functions.https.onRequest(app);