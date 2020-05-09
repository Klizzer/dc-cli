addEventListener("fetch", e => {
    e.respondWith(handleRequest(e.request));
});

async function handleRequest(request) {
    return await fetch(request);
}