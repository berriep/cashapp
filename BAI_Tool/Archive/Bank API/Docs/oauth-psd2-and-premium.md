Skip to main content
Rabobank Developer Portal
Get startedAPI DocumentationAPI StatusSupport
Barry Peijmen
Barry Peijmen
 
You are viewing our Sandbox environment. Click here for more information.

OAuth - PSD2 and Premium
Rabobank's PSD2 and Premium APIs require authorization, we offer the following authorization APIs:

PSD2 - OAuth 2.0
Premium - Authorization services, which is an OAuth 2.0 implementation for premium APIs.
The difference between these APIs is the consent days:

PSD2 - Consent is valid for 180 days.
Premium - Consent is valid indefinitely until revoked.
These APIs enable your application to communicate with Rabobank, on behalf of a user, while keeping their usernames, passwords, and other information private.

If an API requires access authorization, this is specified in the API reference as Type: OAuth2.

The Consent Details Service API provides information about user consent to third parties, if authorized by the end user.

Before you begin
Make sure you have a working Sandbox account in the Rabobank developer portal. Read Get Started to set up an account and register an Application.

After your account set up is complete, configure the OAuth 2.0 redirect URI for your application.

If you have more than one redirect URL listed in the developer portal, make sure to provide the same redirect URI (as provided during registration) in the redirect_uri query parameter during an Authorization call.

Next, subscribe the API you want to use and the Authorization Services API to your application.

A subscription to these APIs is only required if you want to use the Consent Details Service API.

This API provides the services needed to obtain tokens for OAuth 2.0 secured APIs, Endpoint:

Premium - https://oauth.rabobank.nl/openapi/oauth2-premium/
PSD2 - https://oauth.rabobank.nl/openapi/oauth2/
OAuth authorization flow
Applications using APIs, which require access authorization should follow the OAuth authorization code flow. This flow gives your application access to the required resources, if approved by the user.

rabo_oauth_authorization_flow

Request Authorization
Your application identifies the permissions (known as scopes) it requires from the resource owner and requests authorization to access their private information.

Example:

A user wants to use your application for creating payment requests, but they don’t want to share their account balance with it. The scopes your application identifies and requests, enables:

Your application to request access to the needed API resources.
User to control the amount of access they grant to your application.
Rabobank provides the end-users information to recognize the authorization page and avoid fraud. Embedding this page results in difficulty of recognition and is considered a security breach. If this happens, your account is placed under review for further deliberation. To avoid this consequence, you should not embed Rabobank's authorization page in your application.

Your application redirects the user to the following path:

https://oauth.rabobank.nl/openapi/oauth2/authorize?response_type=code&scope=SCOPES&client_id=CLIENT_ID&state=SOME_IDENTIFIER

https://oauth.rabobank.nl/openapi/oauth2-premium/authorize?response_type=code&scope=SCOPES&client_id=CLIENT_ID&state=SOME_IDENTIFIER


The request contains the:

Parameters that identify your application.
Permissions the user grants to your application.
URI Parameter	Required?	Example	Description
response_type*	Yes	response_type=code	
This indicates the authorization grant flow.

Value = code. Other response types are currently not supported.

scope	Yes	scope="bai.accountinformation.read”	Scopes represent permissions to resources, which your application requests from the user. Your application can request multiple scopes using a space separated list.
client_id*	Yes	ab588acc-2ac4-446c-abdd-06c2ea8b097a	
This ID uniquely identifies your registered application.

The client_id is registered at Rabobank developer portal.

state	No	ABC123def567	
An opaque value to maintain state while the user is being redirected back and forth between your application and Rabobank.

The  state parameter should be used to prevent CSRF attacks.

redirect_uri	No	your.redirect.url	
Required only if you have more than one redirect_uri in your application.

If you add multiple redirect_urls to your application in the portal, you should use this parameter to identify, which redirect_url should be used for a specific call.

State parameter
You can use the state parameter to maintain state during the authorization code flow and prevent Cross Site Request Forgery (CSRF) attacks.

Use a strong value to prevent malicious users from being able to manipulate the state parameter.

User side:

A window is displayed, asking the user to give their consent and grant access to your application.

Login and consent
The user decides whether or not to grant the permissions to your application. After the user's decision, the Rabobank OAuth server redirects the user agent back to your application using the URL available in Application details in the Rabobank developer portal.

If the user approves the access request, a response is sent from the Rabobank OAuth server to your application's URL containing an authorization code and state parameter:

https://your.redirect.url?code=AUTHORIZATION_CODE&state=SOME_IDENTIFIER
For PSD2 - Their consent is valid for 180 days.

For Premium - Their consent is valid indefinitely until revoked.

If an active consent already exists, a new active consent is created. The consent is not overwritten by the additional consent from the same user.

The user can choose to revoke their consent at any time. If they do this, it voids the access token and the refresh token. If the consent has been revoked or expired, your application receives a response with a 403 Forbidden CONSENT_INVALID status. You can gain access again by requesting authorization from the user.

You may use the Consent Details Service API to see the status of the consent and verify if it has been revoked.

If the user does not approve the request, then the response from the Rabobank OAuth server to your application's URL contains an ‘access_denied’ error message.

https://your.redirect.url?error=access_denied
If the user cancels before authorizing the request, then the response from the Rabobank OAuth server to your application's URL contains an ‘access_denied’ error message.

https://your.redirect.url?error=access_denied
In case of an approval, the API redirects the user to your application, together with an authorization code. Your application uses this authorization code to request an access token from the Rabobank API(s) you want to use.

The authorization code is valid for a short time and can only be used once. Your application should use the code immediately after receiving, it is not necessary to store this code.

Access and Refresh tokens
After your applications server receives an authorization code, it can exchange it for an access token by making a request to the Rabobank server.

Your application must include the access token in all API requests, which require authorization from the user.

Access tokens should be stored securely and only be used on the server side. If your application doesn't require constant access, we recommend to not store the access token.

rabo_oauth_refresh_flow

Access token
Your application requests an access token in exchange for the authorization code by making a POST request to one of the following endpoints:

PSD2 - /oauth2/token
Premium - /oauth2-premium/token
POST /oauth2/token Headers: Content-Type: application/x-www-form-urlencoded Authorization: Basic BASE64(CLIENT_ID + ":" + CLIENT_SECRET) Body (x-www-form-urlencoded): grant_type: authorization_code code: AUTHORIZATION_CODE
POST /oauth2-premium/token Headers: Content-Type: application/x-www-form-urlencoded Authorization: Basic BASE64(CLIENT_ID + ":" + CLIENT_SECRET) Body (x-www-form-urlencoded): grant_type: authorization_code code: AUTHORIZATION_CODE
This request is secured with Basic authentication using your application's:

Client ID as username
Client Secret as password
Example:

Client id - ab588acc-2ac4-446c-abdd-06c2ea8b097a    
Client secret - J6aA1fL8vJ6xV0iI5bX4nR4nA8pK7dG3cI0jK5mR6rN2qQ3pP0

Authorization: Basic YWI1ODhhY2MtMmFjNC00NDZjLWFiZGQtMDZjMmVhOGIwOTdhOko2YUExZkw4dko2eFYwaUk1Ylg0blI0bkE4cEs3ZEczY0kwaks1bVI2ck4ycVEzcFAw
The client secret should be stored securely and should only be used on the server side.

Parameter	Example	Description
grant_type	authorization_code	This states a method that an application can use to obtain an access token. There are several types of grants, which allow different types of access.
code	2aks1bVI2ck4ycVEzcFAwYUExZkw4dko2eFYwaUk1Ylg0blI0bkE 4cEs3ZEczY0kwYWI1OD hhY2MtMmFjNC00NDZjLWFiZGQtMDZ jMmVhOGIwOTdhOko	The AUTHORIZATION_CODE your application received in the previous step.
In requests to Rabobank APIs secured with OAuth 2.0, you must add the access token in the Authorization header:

Authorization: Bearer ACCESS_TOKEN
If the token has expired or the consent has been revoked, your application receives a response with a 401 Unauthorized status.

Refresh token
If the access token expires you can obtain a new one by using the refresh token, you receive this token with the access token.

To avoid unsuccessful requests, your application should refresh the token before it expires.

Your application can use the refresh token to obtain a new access token if:

The user's consent has not been revoked, and
The refresh token has not expired.
This token can only be used on the server side an is valid for one time use. This token should be stored securely.

The access token has a refresh limit of 4096 times. When this limit is met, a new consent is required from the end user.

Request examples:

POST /oauth2/token Headers: Content-Type: application/x-www-form-urlencoded Authorization: Basic BASE64(CLIENT_ID + ":" + CLIENT_SECRET) Body (x-www-form-urlencoded): grant_type: refresh_token refresh_token: REFRESH_TOKEN
POST /oauth2-premium/token Headers: Content-Type: application/x-www-form-urlencoded Authorization: Basic BASE64(CLIENT_ID + ":" + CLIENT_SECRET) Body (x-www-form-urlencoded): grant_type: refresh_token refresh_token: REFRESH_TOKEN
Parameter	Example	Description
grant_type	refresh_token	This states a method that an application can use to obtain an access token. There are several types of grants, which allow different types of access.
refresh_token	tGzv3JOkF0XG5Qx2TlKWIA	The REFRESH_TOKEN that your application received with the previous set of tokens.
Your application can use the new access token to make requests to Rabobank API(s).

Receive Tokens
The response contains the access token, the refresh token, and their expiration times in seconds and the timestamp when consent was given.

The access and refresh token length and lifetime values received in the response should not be hardcoded, these values received are dynamic and subject to change.

{ "token_type": "bearer", "access_token": ACCESS_TOKEN, "expires_in": 86400, "consented_on": 1507267950, "metadata": "a:consentId 123a1a2a-888c-4015-8099-f88b080d0bbb", "scope": SCOPES, "refresh_token": REFRESH_TOKEN, "refresh_token_expires_in": 2592000 }
Security Considerations
The client secret, access token, refresh token, and data that you have retrieved from Rabobank API(s) is sensitive. They need to be handled carefully, we recommend following the guidelines below:

Keep tokens and client secret server side
The client secret of your application, the access and refresh tokens should never be sent out to the user (or be available to them).

Any traffic that sends one of these items should be server to server communication.

Use HTTPS
You are required to handle the transport of codes and tokens so we recommend using HTTPS to prevent hackers.

Type of Communication	Requirement
Server – server communication	When connecting your servers to the Rabobank API server, the use of HTTPS is required. You should always check the validity of our Rabobank HTTPS certificate.
Client – server communication	
When your client connects to your server:

make use of HTTPS to prevent hacking e.g. using a rogue wifi access point. You should always check the certificate during the HTTPS handshake. It should be your certificate and it should be valid, this is also known as certificate pinning.
make use of HTTP Strict Transport Security (HSTS). This ensures that the client always uses HTTPS to the specified domain.
Redirect to the preferred browser of the user
The authorization page as mentioned in the OAuth authorization flow must be opened in the user's preferred browser. Rabobank users can only confirm that they are authenticating with the genuine Rabobank site if they have the tools provided by their browser, such as the URL bar and Transport Layer Security (TLS) certificate information.

Redirecting authorization in native applications
For native applications, our authorization page must be opened in the default browser. Rabobank users can only confirm that they are authenticating with the genuine Rabobank site if they have the tools provided by their browser, such as the URL bar and Transport Layer Security (TLS) certificate information.

Native applications can use custom URL schemes as redirect URIs, to redirect the user back from the browser to the application that is requesting permission.

Any attempt to embed Rabobank’s authorization page results in your application being permanently banned from using Rabobank APIs.

Use Stable OAuth 2.0 Client Libraries
We recommend using stable libraries for performing the OAuth 2.0 process, instead of implementing your own.

If we suspect that your application has been compromised or detect suspicious activity, your application could be permanently banned from using Rabobank APIs.

Keep your application and its environment secure
Your application should be protected against cross site scripting, SQL injection, and other OWASP top 10 vulnerabilities. This is to prevent the leakage of sensitive information.

We recommend you do security tests on your application and its environment at regular intervals and with every major change.

Storing sensitive information
Make sure that only your application is capable of saving any sensitive information. We recommend you encrypt any sensitive data, and limit the authorization of storage devices.

More information on OAuth 2.0
For more information on OAuth 2.0, see:

Oauth 2.0 tutorial Explain Like I’m 5 (external website)
OAuth 2.0 RFC 6749, section 4.1 (external website)
Troubleshooting common OAuth 2.0 related problems
See OAuth2 error and troubleshooting guide for information on common problems and solutions related the connection to the Rabobank OAuth 2.0 flow.

Other Rabobank developer services that might be interesting for you.

A Digital Identity Service Provider (DISP) that offers a range of online login, identity, signature and archiving solutions.

 Go to Rabobank Identity Services.
One Dashboard for your payment solutions, transaction management and reporting tools for webshops and payment terminals.

 Go to Rabo OmniKassa
© 2025 Rabobank
Privacy statement
Cookies
Terms of Use
