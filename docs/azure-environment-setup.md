# Setting Up Your Azure Cloud Resources

![Deploy to Azure](https://aka.ms/deploytoazurebutton)

## Introduction

This demonstration of the Enclave Device Blueprint for Confidential Computing makes use of a number
of Azure cloud resources that you will need to have set up and running in a suitable Azure
subscription in order to run the demo from end to end. The blueprint is very flexible and can be
adapted to other cloud environments, but this demonstration will make use of Azure services.

If you do not yet have a Microsoft Azure account, you can [get started with a free
account](https://azure.microsoft.com/en-gb/free/).

Once you have your Azure subscription, begin by creating a [resource
group](https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/overview) as a
convenient way to house all of the resources that you are going to create for this demonstration.

The other resources that you will use in this demonstration include:

- An [Azure IoT Hub](https://docs.microsoft.com/en-us/azure/iot-hub/), which you will use to handle
   the connection and messaging between the cloud and your enclave device(s).
- An [Azure Key Vault](https://docs.microsoft.com/en-us/azure/key-vault/), which you will use to
   store the one-time encryption keys (also known as *class keys*) between your build environment
   and your enclave device(s). This demonstration uses
   [secrets](https://docs.microsoft.com/en-us/azure/key-vault/secrets/) in Azure Key Vault as the
   storage mechanism for keys.
- An [Azure Function App](https://docs.microsoft.com/en-us/azure/azure-functions/), which will run
   some simple management functions to create and share keys that are stored in the Azure Key Vault
   secret store. Demonstration source code for these functions is provided with this sample. As a
   developer, you have complete control over the management of encryption keys for confidential
   computing. The functions provided with this sample are examples, but you could replace them with
   your own implementations to suit your preferred key lifecycle and storage designs, provided that
   they follow the documented contracts.
- An [Azure Container Registry](https://docs.microsoft.com/en-us/azure/container-registry/), which
   will host the complete workload for this demonstration, including the encrypted confidential
   package. Containers are a convenient and scalable way for distributing workloads onto edge
   computing devices.

The remainder of this document will show you how to create and configure these resources in
preparation for running the demonstration.

## Creating Your Azure IoT Hub

To create your IoT Hub in Azure, simply follow the Azure documentation pages. You can create an IoT
Hub using [the Azure
portal](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-create-through-portal) or the [Azure
CLI](https://docs.microsoft.com/en-us/azure/iot-hub/quickstart-send-telemetry-cli).

## Registering Your Enclave Device

To register your IoT Edge device in the IoT Hub, follow the procedure documented
[here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-register-device?view=iotedge-2020-11&tabs=azure-portal).

## Creating Your Azure Key Vault

Create your Azure Key Vault using [the Azure
portal](https://docs.microsoft.com/en-us/azure/key-vault/general/quick-create-portal) or [the Azure
CLI](https://docs.microsoft.com/en-us/azure/key-vault/general/quick-create-cli).

## Creating Your Function App

You can create a new, empty function app using [the Azure
CLI](https://docs.microsoft.com/en-us/cli/azure/functionapp?view=azure-cli-latest#az_functionapp_create)
or by selecting the Function App service in the Azure web portal and using the **+ Create** button
to create a new function app resource.

Example source code for the function app is bundled with this demonstration in the
**src/functionapp** folder. You can use these example sources just as they are in order to run the
demonstration. Each example function is supplied as a C#-script source file. Three functions are
required: **ProvisionKey**, **WrapKey** and **DistributeKey**. The sample contains a subfolder for
each of these functions, complete with a `run.csx` file, which contains the function implementation,
and a `function.proj` file, which contains information about the function and its dependencies.

The **ProvisionKey** and **WrapKey** functions are both web contracts that can be called as RESTful
APIs by clients on demand. Therefore, when creating these functions, you should configure them to be
[HTTP trigger](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=csharp)
functions.

The **DistributeKey** function is somewhat different. It is not called explicitly. It runs as a
background housekeeping task, synchronizing encryption keys with all of the edge devices in your
deployment. There are different ways of triggering this function so that it runs at suitable times.
However, the simplest and easiest is probably just to run it on a regular basis, such as once every
hour. You can set it up as a
[timer trigger](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=csharp)
to achieve this with very little effort. With a little more work, it would be possible to use event
triggers to ensure that the function is called as needed on a more precise basis, such as when any
new key is added to the vault, or whenever a new edge device registers or publishes a new public key.
Such enhancements are left as an exercise for the reader.

## Giving Your Function App The Correct Permissions

The Azure functions that run within the function app are responsible for provisioning, fetching and
distributing encryption keys. In this demonstration, encryption keys are managed and stored as
secrets within the Azure Key Vault resource. Therefore, the functions will need to be authorised to
read and write secrets within the Key Vault. This permission is not granted by default, so you will
need to use the managed identity subsystem of Azure to create an identity for your function app, and
then add an access policy to the key vault that grants the required rights to that identity.

The procedure for adding a new managed service identity to your function app is documented
[here](https://docs.microsoft.com/en-us/azure/app-service/app-service-managed-service-identity#creating-an-app-with-an-identity).
You can use a system-assigned identity in this demonstration.

Once you have created the managed service identity for the function app, the next step is to add an
access control policy to the key vault. You can do this using [the Azure
portal](https://docs.microsoft.com/en-us/azure/key-vault/general/assign-access-policy-portal) or
[the Azure CLI](https://docs.microsoft.com/en-us/azure/key-vault/general/assign-access-policy-cli).

## Connecting Your Function App To IoT Hub And Key Vault

In addition to configuring the permissions above, you will also need to provide the function app
with some additional configuration settings so that it can talk to both the key vault and the IoT
hub. You will need to provide the function app with a connection string for the IoT hub, and an
endpoint URL for the key vault. These are done using application settings, which are similar to
environment variables. Learn about application settings
[here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal).

You need to add the following *two* new configuration settings to the function app:

- **HubConnectionString** - this is the connection string for the IoT Hub resource. Its value will
   be something like
   `HostName=<IOT_HUB_NAME>.azure-devices.net;SharedAccessKeyName=TestPolicy;SharedAccessKey=<ACCESS_KEY`.
   You can retieve the correct connection string by viewing the properties of your IoT Hub in the
   Azure portal, or by using [the
   CLI](https://docs.microsoft.com/en-us/cli/azure/iot/hub/connection-string?view=azure-cli-latest).
- **KeyVaultEndpoint** - this is the URL that provides the Azure Key Vault REST API for your key
   vault instance. The URL is specific to a single key vault, and hence it needs to be configured.
   Its form will be something like `https://<KEY_VAULT_NAME>.vault.azure.net/`. No additional
   parameters or authentication values are needed, because the function app already has its own
   managed service identity (see above), and you will already have granted it with the required
   permissions to call into the key vault.

## Provisioning An Encryption Key

Once you have created your Key Vault and Function App, and populated the code for the functions,
you can try calling the **ProvisionKey** function to create a symmetric encryption key within the
vault. This is an example of a *class key*, which can be used to encrypt your confidential
applications.

All encryption keys need a name or identity. This is simply a string that allows the tools to
locate and use the correct key for any given application. In this sample, we will use the identity
`1f574668-6c89-41b5-b313-4b2d85d63c9d`, which corresponds to the UID of the OpenEnclave Machine
Learning demo. Make sure that you use exactly this string, rather than generating your own UID. It
needs to match the demo application that you will be using. If you wish to provision your own keys
for other workloads, then you will need to use your own suitable key identity.

You can call the **ProvisionKey** function in a variety of ways. You can call it from within the
Azure Portal and supply the following JSON data as the request body:

```
{
   "key_name": "1f574668-6c89-41b5-b313-4b2d85d63c9d"
}
```

But because this is simply an HTTP web hook, you can call it using any suitable HTTP client on any
machine, such as [Postman](https://www.postman.com) or [curl](https://linux.die.net/man/1/curl).
To do this, you will need a suitable HTTP endpoint to call your function. The simplest way to do
this is with a function access key. Use the "Get Function URL" feature of the Azure Portal, or
use [the CLI](https://docs.microsoft.com/en-us/cli/azure/functionapp/function/keys?view=azure-cli-latest)
to obtain a function key for **ProvisionKey**. Here is an example of calling the **ProvisionKey**
function using curl:

```
curl --header "Content-Type: application/json" \​
    --request POST \​
    --data '{"key_name": "1f574668-6c89-41b5-b313-4b2d85d63c9d"}’ \
    https://<FUNCTION_APP>.azurewebsites.net/api/ProvisionKey?code=<ACCESS_CODE>​
```

(where `<FUNCTION_APP>` and `<ACCESS_CODE>` are the correct host and function key for your
environment).

If the function succeeds, you should see that your Key Vault now has a new secret whose name is
`1f574668-6c89-41b5-b313-4b2d85d63c9d`. This is the class encryption key that will be used in the
demo.

If the function fails, it indicates a problem with your environment set-up. Double check that you
have correctly followed the procedures above. Check in particular that your Function App has the
required access to the Key Vault, and that the **KeyVaultEndpoint** setting is correct in your
app settings.

## Configuring The Key For Distribution

In order to configure the **DistributeKey** function to distribute the new encryption key to your
registered edge devices, you will need to add one final configuration setting to the Function App.
Using the same procedure as you used above to configure the **HubConnectionString** and
**KeyVaultEndpoint** settings, create an additional setting called **KeyVaultSecretNames** and
set its value to `1f574668-6c89-41b5-b313-4b2d85d63c9d`. (If you want to distribute multiple keys,
then you should use a comma-separated list of values here. But for the demo, you will only need to
distribute this single key).

## Creating Your Container Registry

Follow the Azure documentation pages to create a container registry using either [the Azure
portal](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal)
or [the Azure
CLI](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-azure-cli).

## You're All Set!

Congratulations! You now have a set of Azure cloud resources and you are ready to run the
demonstration for the enclave device blueprint for confidential computing at the edge!
