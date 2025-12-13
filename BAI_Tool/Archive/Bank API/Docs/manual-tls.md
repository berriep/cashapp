Skip to main content
Rabobank Developer Portal
Get startedAPI DocumentationAPI StatusSupport

Connect to Rabobank API servers
All Rabobank APIs use the HTTPS protocol (HTTPS = HTTP over TLS). Before sending an HTTP request your client application must make a TLS connection to a Rabobank API server.

During the setup of this connection, the client should validate the server certificate of the Rabobank API server.

For all APIs on domain api.rabobank.nl, an extra layer of security is added, the client must provide a Client Certificate to be validated by the Rabobank server to authenticate the client sending the HTTP request.

It may happen that the API you want to use is on a different domain and does not require Mutual TLS, we advise to check the API reference before proceeding

Validate Rabobank server certificate
The server certificates of both api.rabobank.nl and apin.rabobank.nl are EV certificates (EV = Extended Validation). To be able to validate this certificate during the TLS handshake your application must trust the Root CA that issued the certificate. The server certificate is issued by USERTrust ECC Certification Authority.

In the future, Rabobank may start using QWAC certificates issued under the European eIDAS trust scheme. We will inform you timely and accordingly.

Get a client certificate
If your HTTP client sends an API request to api.rabobank.nl, it must provide a Client Certificate during TLS handshake. The type of API defines which type of Client Certificate should be used:

PSD2: An eIDAS QWAC certificate issued by the Qualified Trust Service Provider of your choice.

Premium: An EV Client Certificate complying to the following requirements:

Issued by a CA listed in the Mozilla CA Certificate report
When an RSA key is used: key length should be at least 2048-bit
Maximum validity period of one year.
For the Sandbox environment, you can use any certificate as a TLS Client Certificate (this certificate always passes the validation).

We provide you with a key and certificate that you can use in Sandbox: key.pem and cert.pem.

Register your Client Certificate at Rabobank Developer Portal
Create an application on the Rabobank Developer portal.

To select an existing certificate or add a new certificate:

Log into the Rabobank developer portal.
Click My Apps.
Select the application (app) that should make the TLS protected requests.
Click Select or Add a new certificate.
Select certificate from the displayed options (all your uploaded certificates are available here) or Upload a new certificate.
Add Your MTLS Certificate and Certificate name.

The format must comply to the Textual Encoding of Certificates, you must copy and paste the whole chain of certificates, not just the leaf certificate

Click Save.
Certificate Status and Types

We currently validate and accept the following certificate classifications: PSD2 QWAC, PSD2 Qseal, and EV SSL.

If your certificate does not fall under the aforementioned classifications, it is labeled as Unknown certificate type, contact our Support team if you want to use these certificates.

Status

Valid - The certificate chain has passed our validations and is accepted.
Expired - The certificate has expired and should be renewed.
Not validated - The certificate is not validated, you can try to upload your entire certificate chain again. If it still returns an error, you need to renew the certificate, see Client Certificate renewal below.
Configure your software
Server validation
For your application, to be able to validate the Rabobank server certificate your application must use a Truststore that contains the Root CA’s as mentioned in Validate Rabobank server certificate.

You application must implement the SNI extension (Server Name Indication).

Note: Not using SNI results in unexpected responses.

Provide Client Certificate
For api.rabobank.nl your application must be able to provide a Client Certificate during TLS handshake.

You should provide the intermediate certificate(s), called the certificate chain. This helps Rabobank servers to validate your certificate.

Client Certificate renewal
You can replace your TLS certificate using the following steps:

Navigate to the Rabobank Developer Portal → My Organization(s) → My Apps.
Select the application for which you want to replace the TLS certificate and select the Configuration tab.
Click Edit Certificate and copy and paste your TLS certificate in the certificate field. The format must comply to the Textual Encoding of Certificates and you must copy and paste the whole chain of certificates, not just the leaf certificate.
Click Save. 
You can only register one Client Certificate per application. If you want to change your Client Certificate on the Rabobank developer portal, make sure to configure the new certificate in your application.

Other Rabobank developer services that might be interesting for you.

A Digital Identity Service Provider (DISP) that offers a range of online login, identity, signature and archiving solutions.

 Go to Rabobank Identity Services.
One Dashboard for your payment solutions, transaction management and reporting tools for webshops and payment terminals.

 Go to Rabo Smart Pay
© 2025 Rabobank
Privacy statement
Cookies
PSD2 availability
Sandbox terms of use
