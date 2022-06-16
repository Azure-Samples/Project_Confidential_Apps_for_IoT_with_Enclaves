# Testing

```
$ openssl req -x509 -sha256 -nodes -newkey rsa:2048 -keyout key.pem -days 730 -out public.pem
```

```
$ openssl asn1parse -in public.pem -out asn1.der
```

```
$ base64 asn1.der
```

```
```