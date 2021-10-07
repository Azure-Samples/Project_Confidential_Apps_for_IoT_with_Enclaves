# Setting Up Your Azure Cloud Resources

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

## Creating Your Container Registry

Follow the Azure documentation pages to create a container registry using either [the Azure
portal](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal)
or [the Azure
CLI](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-azure-cli).

## You're All Set!

Congratulations! You now have a set of Azure cloud resources and you are ready to run the
demonstration for the enclave device blueprint for confidential computing at the edge!
