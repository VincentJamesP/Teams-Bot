import { useEffect, useRef, useState } from "react"
import { constants } from './constants'
import * as teams from '@microsoft/teams-js'
import { teamsDarkV2Theme, teamsTheme, teamsHighContrastTheme } from '@fluentui/react-northstar'
import { themeNames } from "@fluentui/react-teams"


export const usePrevious = (value) => {
    const ref = useRef()
    useEffect(() => {
        ref.current = value
    })
    return ref.current
}

export const initTeamsContext = async () => {
    try {
        await teams.app.initialize()
        constants.context = await teams.app.getContext()
        teams.app.notifyAppLoaded()
        console.log('Teams context successfully retrieved.')
        return true
    } catch (e) {
        console.error(e)
        console.error('Unable to retrieve Teams context. This web view must be opened from within the corresponding App in Teams.')
        return false
    }
}

export const handleSetTheme = (theme, update) => {
    theme = theme || ''
    switch (theme) {
        case 'contrast':
            update(themeNames.HighContrast)
        case 'dark':
            update(themeNames.Dark)
        case 'default':
        default:
            update(themeNames.Default)
    }
}
