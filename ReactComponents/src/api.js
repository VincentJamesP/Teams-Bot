import user from '@microsoft/teams-js'
const axios = require('axios').default

import { constants } from './constants.js'

/** Fetch the duties of the specified user
 * @param id the AadUserId of the user, taken from app.getContext(). if empCode is ommitted, the API will return the duties of this user.
 * @param empCode optional; if provided, return duties of the crew member with this employee code.
 * @param query optional; if provided, only return duties where the label fully or partially matches.
 * @returns the list of objects returned by the API.
 * */
export const fetchDuties = async ({ id = constants.testAadUserId, empCode = '', query = '' } = {}) => {
    const url = `${constants.api}/extensions/search`

    try {

        const res = await axios.get(url, {
            headers: {
                'Access-Control-Allow-Origin': '*',
                'Content-Type': 'application/json',
            },
            params: { type: 'dutiesOf', id: id, empCode: empCode, query: query }
        })

        console.log(`GET ${res.request.responseURL}`);
        return res.data
    } catch (e) {
        console.error(e)
        return []
        // return { isError: true, error: e }
    }
}

/** Fetch the users
  * @param id the AadUserId of the user, taken from app.getContext().
 * @param query optional; if provided, only return users whose name fully or partially matches.
 * @returns the list of objects returned by the API.
 */
export const fetchUsers = async ({ id = constants.testAadUserId, query = '' } = {}) => {
    const url = `${constants.api}/extensions/search`

    try {
        const res = await axios.get(url, {
            headers: {
                'Access-Control-Allow-Origin': '*',
                'Content-Type': 'application/json',
            },
            params: { type: 'users', id: id, query: query }
        })

        console.log(`GET ${res.request.responseURL}`);
        return res.data
    } catch (e) {
        console.error(e)
        return []
        // return { isError: true, error: e }
    }
}

export const submitRequest = async (request) => {
    const url = `${constants.api}/extensions/submit`
    try {
        const res = await axios.post(url, request, {
            headers: {
                'Access-Control-Allow-Origin': '*',
                'Content-Type': 'application/json',
            }
        })

        return true
    } catch (e) {
        console.error(e)
        return false
    }
}


export const fetchReserve = async ( err) => {
    const url = `${constants.api}/tabs/reserve`
    try {

        const res = await axios.get(url, {
            headers: {
                'Access-Control-Allow-Origin': '*',
                'Content-Type': 'application/json',
            }
        })
        console.log(`GET ${res.request.responseURL}`);
        return res.data
    } catch (e) {
        console.error(e)
        err(e)
        return []
    }
}