import React from 'react'
import ReactDOM from 'react-dom'
import { useState, useEffect } from 'react'
import regeneratorRuntime from "regenerator-runtime";
import { Provider, teamsTheme, Flex, Form, FormDropdown, FormButton, Header } from '@fluentui/react-northstar'
import * as teams from "@microsoft/teams-js";

import { constants } from '../constants'
import { fetchDuties, fetchUsers, submitRequest } from '../api'
import { usePrevious } from '../common'

const SwapForm = (props) => {
    const [preventSubmit, setPreventSubmit] = useState(true)
    const [submitting, setSubmitting] = useState(false)
    const [submitted, setSubmitted] = useState(false)

    const [searchOffer, setSearchOffer] = useState("")
    const [offerLoading, setOfferLoading] = useState(true)

    const [searchUser, setSearchUser] = useState("")
    const [userLoading, setUserLoading] = useState(true)

    const [searchReceive, setSearchReceive] = useState("")
    const [receiveLoading, setReceiveLoading] = useState(true)

    const [selectedOffer, setSelectedOffer] = useState()
    const [selectedUser, setSelectedUser] = useState()
    const prevUser = usePrevious(selectedUser)
    const [selectedReceive, setSelectedReceive] = useState()


    const onCancel = (event) => {
        teams.dialog.submit(null, constants.messagingExtensionBotId)

        event.preventDefault()
        event.stopPropagation()
        event.nativeEvent.stopImmediatePropagation()
        console.log('cancelled task.');
        return true
    }

    const onSubmit = async (event) => {
        console.log('submitting...');
        setSubmitting(true)

        setSubmitted(
            await submitRequest({
                initiator: constants.context.user.id,
                offer: selectedOffer,
                swapWith: selectedUser,
                receive: selectedReceive
            })
        )
        // var obj = { type: 'exchanges', offer: selectedOffer, swapWith: selectedUser, receive: selectedReceive }
        // teams.dialog.submit(obj, constants.appId)
        console.log('submitted.');
        return true
    }


    // call search api when filtered results are less than 10
    useEffect(() => {
        setOfferLoading(true)
        const timeout = setTimeout(() => {
            if (props.offers.items && props.offers.items.filter(i => i.content.includes(searchOffer) || i.header.includes(searchOffer)).length <= 4)
                props.offers.update(setOfferLoading, searchOffer)
        }, 1000)
        return () => clearTimeout(timeout)
    }, [searchOffer])

    useEffect(() => {
        setUserLoading(true)
        const timeout = setTimeout(() => {
            if (props.users.items.filter(i => i.content.includes(searchUser) || i.header.includes(searchUser)).length <= 4)
                props.users.update(setUserLoading, searchUser)
        }, 1000)
        return () => clearTimeout(timeout)
    }, [searchUser])

    useEffect(() => {
        setReceiveLoading(true)
        const timeout = setTimeout(() => {
            if (selectedUser && props.receives.items.filter(i => i.content.includes(searchReceive) || i.header.includes(searchReceive)).length <= 4)
                props.receives.update(setReceiveLoading, selectedUser.key, searchReceive)
        }, 1000)
        return () => clearTimeout(timeout)
    }, [searchReceive])

    // get receiving duties and reset selectedReceive when selectedUser changes
    useEffect(() => {
        if (selectedUser && selectedUser !== prevUser)
            props.receives.update(setReceiveLoading, selectedUser.key, searchReceive)
    }, [selectedUser])

    // allow submitting when all three fields have been filled out
    useEffect(async () => {
        setPreventSubmit(selectedUser == null || selectedUser == null || selectedReceive == null)
    }, [selectedOffer, selectedUser, selectedReceive])


    if (submitted)
        return <Header as='h2'
            // align='center'
            content='Request submitted'
            description='You may now close this window.'
        />

    return <Form
        id='kpt-schedule-exchanges'
        onSubmit={onSubmit}
    >
        <FormDropdown fluid search clearable
            id='kpt-offer'
            label='Schedule to Offer'
            items={props.offers.items}
            searchQuery={searchOffer}
            onSearchQueryChange={(e, data) => setSearchOffer(data.searchQuery)}
            value={selectedOffer || undefined}
            onChange={(e, data) => setSelectedOffer(data.value)}
            loading={offerLoading}
            loadingMessage='Fetching duties...'
            noResultsMessage='No results.'
            placeholder='Search by flight number'
        />
        <FormDropdown fluid search clearable
            id='kpt-users'
            label='Swap With'
            items={props.users.items}
            searchQuery={searchUser}
            onSearchQueryChange={(e, data) => setSearchUser(data.searchQuery)}
            value={selectedUser || undefined}
            onChange={(e, data) => setSelectedUser(data.value)}
            loading={userLoading}
            loadingMessage='Fetching users...'
            noResultsMessage='No results.'
            placeholder='Search by name or email'
        />
        <FormDropdown fluid search clearable
            id='kpt-receive'
            label='Schedule to Receive'
            items={props.receives.items}
            searchQuery={searchReceive}
            onSearchQueryChange={(e, data) => setSearchReceive(data.searchQuery)}
            value={selectedReceive || undefined}
            onChange={(e, data) => setSelectedReceive(data.value)}
            loading={receiveLoading}
            loadingMessage='Fetching duties...'
            noResultsMessage='No results.'
            disabled={selectedUser == null}
            placeholder={selectedUser == null ? 'Select a user to swap with first' : 'Search by flight number'}
        />

        <Flex gap='gap.small' hAlign='end' >
            {/* <List> */}
            {/* <FormButton type='submit' primary disabled={preventSubmit} styles={{ 'display': 'none' }} />
            <FormButton type='submit' onClick={onCancel} content='Cancel' secondary /> */}
            <FormButton type='submit' content='Submit' primary disabled={preventSubmit} loading={submitting} />
            {/* </List> */}
        </Flex>
    </Form>
}


const SwapDialog = () => {
    const [offers, setOffers] = useState([])
    const [users, setUsers] = useState([])
    const [receives, setReceives] = useState([])

    const updateOffers = async (setLoading, query) => {
        setOffers(await fetchDuties({ query: query }))
        setLoading(false)
    }
    const updateUsers = async (setLoading, query) => {
        setUsers(await fetchUsers({ query: query }))
        setLoading(false)
    }
    const updateReceives = async (setLoading, empCode, query) => {
        setReceives(await fetchDuties({ empCode: empCode, query: query }))
        setLoading(false)
    }


    useEffect(async () => {
        try {
            await teams.app.initialize()
            constants.context = await teams.app.getContext()
            teams.app.notifyAppLoaded()
            console.log('loaded.');
            // teams.dialog.submit(null, constants.messagingExtensionBotId)
            // teams.tasks.submitTask(null, constants.messagingExtensionBotId)
        } catch (e) {
            console.error(e);
            console.error('Can\'t get Teams context; using fallback development AadUserId.');
        }
    }, [])

    return (
        <Provider theme={teamsTheme}>
            <div style={{ 'padding': '1rem 2rem' }}>
                <SwapForm offers={{ items: offers, set: setOffers, update: updateOffers }}
                    users={{ items: users, set: setUsers, update: updateUsers }}
                    receives={{ items: receives, set: setReceives, update: updateReceives }}
                />
            </div>
        </Provider>
    )
}

ReactDOM.render(<SwapDialog />, document.getElementById('root'))
