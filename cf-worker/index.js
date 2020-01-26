'use strict'

/** @type {RequestInit} */
const PREFLIGHT_INIT = {
    status: 204,
    headers: new Headers({
        'access-control-allow-origin': '*',
        'access-control-allow-methods': 'GET,POST,PUT,PATCH,TRACE,DELETE,HEAD,OPTIONS',
        'access-control-max-age': '1728000',
    }),
}

/**
 * @param {any} body
 * @param {number} status
 * @param {Object<string, string>} headers
 */
function makeRes(body, status = 200, headers = {}) {
    headers['access-control-allow-origin'] = '*'
    return new Response(body, { status, headers })
}

/**
 * @param {string} urlStr 
 */
function newUrl(urlStr) {
    try {
        return new URL(urlStr)
    } catch (err) {
        return null
    }
}

addEventListener('fetch', e => {
    const ret = fetchHandler(e)
        .catch(err => makeRes('cfworker error:\n' + err.stack, 502))
    e.respondWith(ret)
})

/**
 * @param {FetchEvent} e 
 */
async function fetchHandler(e) {
    const req = e.request
    const urlStr = req.url
    const urlObj = new URL(urlStr)
    const path = urlObj.href.substr(urlObj.origin.length)

    if (path.startsWith('/http/')) {
        return await httpHandler(req, path.substr(6))
    }

    return makeRes('hello!')
}

/**
 * @param {Request} req
 * @param {string} pathname
 */
async function httpHandler(req, pathname) {
    const reqHdrRaw = req.headers
    if (reqHdrRaw.has('x-jsproxy')) {
        return Response.error()
    }

    // preflight
    if (req.method === 'OPTIONS' && reqHdrRaw.has('access-control-request-headers')) {
        return new Response(null, PREFLIGHT_INIT)
    }

    const reqHdrNew = new Headers(reqHdrRaw)
    reqHdrNew.set('x-jsproxy', '1')

    if (!reqHdrRaw.has('user-agent')) {
        reqHdrRaw.set('user-agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36')
    }
    // cfworker 会把路径中的 `//` 合并成 `/`
    const urlStr = pathname.replace(/^(https?):\/+/, '$1://')
    const urlObj = newUrl(urlStr)
    if (!urlObj) {
        return makeRes('invalid proxy url: ' + urlStr, 403)
    }

    /** @type {RequestInit} */
    const reqInit = {
        method: req.method,
        headers: reqHdrNew,
    }
    if (req.method === 'POST') {
        reqInit.body = await req.arrayBuffer()
    }
    return await proxy(urlObj, reqInit)
}

/**
 * 
 * @param {URL} urlObj 
 * @param {RequestInit} reqInit
 */
async function proxy(urlObj, reqInit) {
    const res = await fetch(urlObj.href, reqInit)
    const resHdrOld = res.headers
    const resHdrNew = new Headers(resHdrOld)

    let status = res.status

    resHdrNew.set('access-control-expose-headers', '*')
    resHdrNew.set('access-control-allow-origin', '*')

    resHdrNew.delete('content-security-policy')
    resHdrNew.delete('content-security-policy-report-only')
    resHdrNew.delete('clear-site-data')

    return new Response(res.body, {
        status,
        headers: resHdrNew,
    })
}