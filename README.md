# Durable Registry

This is a solution for Day 21 of the [25 Days of Serverless](https://25daysofserverless.com/).

## Quick Start

To run the solution locally, be sure you have installed:

- [Azure Functions Core Tools](https://jlik.me/g0h)
- [.NET Core 2.2 or later](https://jlik.me/g0i)
- [Azure Storage Emulator](https://jlik.me/g0j)

Helpful, "good to have":

- [Postman](https://www.getpostman.com/), or
- [HTTP-REPL](https://jlik.me/g0k)

1. Fork the repo (optional)
1. Clone the repo: `git clone https://github.com/JeremyLikness/durable-registry.git` (change to your forked repo if necessary)
1. Navigate to the `src` directory
1. Add a `local.settings.json` and configure it to use the emulator:
    ```json
    {
            "IsEncrypted": false,
            "Values": {
                    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
                    "FUNCTIONS_WORKER_RUNTIME": "dotnet"
            }
    }
    ```
1. Restore, build, and publish the app in one step, execute `dotnet publish`
1. Change to the publish directory: `cd bin\Debug\netcoreapp2.2\publish`
1. Copy the local settings: `cp ../../../../local.settings.json .`
1. Start the Azure Storage Emulator
1. Launch the functions app: `func host start`

## Using the APIs

You can access the APIs directly from your browser or by using your favorite REST client tool (I prefer HTTP-REPL, listed above). All of the solution endpoints use the `GET` verb to make the example as easy as possible to use.

### Open a new registry

`http(s)://{endpoint}/api/Open`

This will return a unique id. You have 5 minutes from the time you open a registry to add items and/or close it. A closed or timed out registry will not accept new entries.

### Add an item to a registry

`http(s)://{endpoint}/api/Add/{id}?item={item name}`

You will receive `200 - OK` if the registry is valid and open.

### Finish (close) a registry

`http(s)://{endpoint}/api/Finish/{id}`

Once closed, you can no longer add items to the registry.

### List the status and items in a registry

`https(s)://{endpoint}/api/Peek/{id}`

If a valid registry id is passed, this will return the status (open or closed) and items in the registry.

### List overall status

`http(s)://{endpoint}/api/Stats`

This will show a count of all registries (open and closed) and a total count of items across all registries.

## Learn More

You can learn more about the solution by reading the related blog post.

👉 [Free Durable Functions Hands-on Lab](https://jlik.me/g0l)
👉 [Durable Functions Documentation](https://jlik.me/g0m)
👉 [Function Entities Documentation](https://jlik.me/g0n)

Regards,

[![Jeremy Likness](https://blog.jeremylikness.com/images/jeremylikness.gif)](https://twitter.com/JeremyLikness)
