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
  

app.get('/login', function(req, res) {
  let querystring = require('querystring')
  res.redirect('https://accounts.spotify.com/authorize?' +
    querystring.stringify({
      response_type: 'code',
      client_id: CLIENT_ID,
      scope: 'user-read-private user-read-email user-modify-playback-state user-read-currently-playing user-read-playback-state',
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
    res.redirect(uri + '?access_token=' + access_token)
  })
})

exports.app = functions.https.onRequest(app);