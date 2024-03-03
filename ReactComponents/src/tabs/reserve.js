import React, { Fragment } from 'react'
import ReactDOM from 'react-dom'
import { useState, useEffect } from 'react'
import { useMediaQuery } from 'react-responsive'
import regeneratorRuntime from "regenerator-runtime";

import { Text, Flex, Button, Input, Popup, Dialog, Checkbox, useStyles, Divider, Header, Loader } from '@fluentui/react-northstar'
import { SearchIcon, TriangleDownIcon, TriangleEndIcon, CloseIcon, FilterIcon } from '@fluentui/react-northstar'
import { Provider as ThemeProvider, themeNames, Table } from '@fluentui/react-teams'
// import { Provider as ThemeProvider, Toolbar, List, themeNames } from '@fluentui/react-teams'
import * as teams from '@microsoft/teams-js'

import { initTeamsContext, handleSetTheme } from '../common'
import { constants } from '../constants'
import { MenuItem } from '@fluentui/web-components'
import { fetchReserve } from '../api'


const listConfig = {
    columns: {
        name: {
            title: 'Name',
            icon: 'ContactCard',
            hideable: true,
            sortable: "alphabetical"
        },
        empCode: {
            title: "Employee Code",
            minWidth: 128
        },
        rank: {
            title: "Rank",
            minWidth: 64,
            hideable: true
        },
        reserve: {
            title: 'Reserve',
            minWidth: 64
        },
        priority: {
            title: 'Priority',
            minWidth: 64
        },
        date: {
            title: 'Date (UTC)',
            minWidth: 64
        }
    }
}


const ReserveDashboard = () => {
    const [theme, setTheme] = useState(themeNames.Default)
    const [data, setData] = useState([])
    const [loading, setLoading] = useState(true)
    const [loadError, setLoadError] = useState()

    const [ranks, setRanks] = useState([... new Set(data.map(d => d.rank))].map((v, i) => { return { id: v, title: v, checked: false } }))
    const [reserve, setReserve] = useState([... new Set(data.map(d => d.reserve))].map((v, i) => { return { id: v, title: v, checked: false } }))
    const [query, setQuery] = useState('')

    const [open, setOpen] = useState(false)
    const [selected, setSelected] = useState()

    useEffect(async () => {
        await initTeamsContext()
        teams.app.registerOnThemeChangeHandler((string) => handleSetTheme(string, setTheme))
        handleSetTheme(constants.context.theme, setTheme)
        let data = await fetchReserve(setLoadError)
        if (data.length)
            setData(data)
        setLoading(false)
    }, [])

    useEffect(() => {
        let ra = ranks.map(r => r.id)
        let re = reserve.map(r => r.id)

        setRanks([... new Set(data.map(d => d.rank))].map((v, i) => { return { id: v, title: v, checked: false } }))
        setReserve([... new Set(data.map(d => d.reserve))].map((v, i) => { return { id: v, title: v, checked: false } }))
    }, [data])

    const isSmallScreen = useMediaQuery({ query: '(max-width: 716px)' })

    const handleInteraction = event => {
        if (event && event.event == 'click') {
            if (event.action == '__details__' || (event.target == 'table' && isSmallScreen)) {
                setSelected(event.subject)
                setOpen(true)
            }
        }
    }

    const checked = ranks.filter(r => r.checked).length + reserve.filter(r => r.checked).length
    const filterText = <Fragment>
        <Text content='Filter' />
        {checked > 0 && <Text style={{ paddingLeft: '0.25rem' }} disabled content={`(${checked})`} />}
    </Fragment>

    let ra = ranks.filter(r => r.checked).map(i => i.id)
    let re = reserve.filter(r => r.checked).map(i => i.id)
    let qu = query.toLowerCase()

    let filtered = data.filter(i => i.name.toLowerCase().includes(qu) || i.empCode.toLowerCase().includes(qu) || i.label.toLowerCase().includes(qu))
    if (ra.length)
        filtered = filtered.filter(i => ra.includes(i.rank))
    if (re.length)
        filtered = filtered.filter(i => re.includes(i.reserve))
    filtered = filtered.slice(0, 50)


    const calcFilterDisabled = () => {
        return !ranks.some(r => r.checked) && !reserve.some(r => r.checked)
    }

    return (
        <ThemeProvider themeName={theme} lang='en-US' >
            <Flex space='between'
                style={{
                    backgroundColor: theme == themeNames.HighContrast ? '#000' : theme == themeNames.Dark ? '#333' : '#fff',
                    position: 'sticky', top: 0, zIndex: 999, padding: '0.5rem 1.25rem', boxShadow: 'rgb(0 0 0 / 13%) 0px 3.2px 7.2px, rgb(0 0 0 / 11%) 0px 0.6px 1.8px'
                }}
            >
                <Flex></Flex>

                <Flex gap='gap.medium'>
                    <Popup align='end' content={
                        <div>
                            <Flex space='between' vAlign='center'>
                                <Text content='Filters' size='small' />
                                <Button size='small' iconOnly text content='Reset' disabled={calcFilterDisabled()}
                                    style={{ opacity: calcFilterDisabled() ? '32%' : '100%' }}
                                    onClick={() => {
                                        let ra = [...ranks]
                                        ra.forEach(i => { i.checked = false; return i })
                                        let re = [...reserve]
                                        re.forEach(i => { i.checked = false; return i })

                                        setRanks(ra)
                                        setReserve(re)
                                    }} />
                            </Flex>
                            <Divider />
                            <Filter title='Ranks' items={ranks} update={setRanks} />
                            <Filter title='Reserves' items={reserve} update={setReserve} />
                        </div>
                    }
                        trigger={<Button text icon={<FilterIcon />}
                            content={filterText}
                        />}
                    />
                    <Input icon={<SearchIcon />} placeholder='Search...' clearable value={query} onChange={(event, data) => setQuery(data.value)} />
                </Flex>
            </Flex>
            <div style={{ zIndex: 0, padding: '2rem 0', height: '100%' }} >
                <Table {...listConfig}
                    style={{ minHeight: '4rem' }}
                    rows={filtered}
                    onInteraction={handleInteraction}
                />
                {loading ?
                    <Loader label='Fetching reserve list, this may take a while...' size='large' />
                    : filtered.length == 0 ?
                        <Flex column hAlign='center'>
                            <Header as='h2' align='center' content={loadError ? 'Unable to fetch Reserve List.' : 'No results found.'} />
                            <Text content={loadError ? `${loadError.code}` : ''} disabled />
                        </Flex>
                        : null
                }
            </div>


            <Dialog
                closeOnOutsideClick={true}
                open={open}
                header={selected && filtered[selected].name}
                headerAction={{
                    icon: <CloseIcon />,
                    title: 'Close',
                    onClick: () => setOpen(false),
                }}
                content={
                    selected &&
                    <div>
                        <Text content='Employee code: ' disabled size="large" /><Text content={filtered[selected].empCode} size="large" /><br />
                        <Text content='Rank: ' disabled size="large" /><Text content={filtered[selected].rank} size="large" /><br />
                        <Text content='Reserve: ' disabled size="large" /><Text content={`${filtered[selected].reserve}${filtered[selected].priority}`} size="large" /><br />
                        <Text content='Date: ' disabled size="large" /><Text content={filtered[selected].date} size="large" />
                    </div>
                }
            />
        </ThemeProvider>
    )
}

const Filter = (props) => {
    const [expanded, setExpand] = useState(false)
    const [visible, setVisible] = useState(false)


    return <div style={{ minWidth: '6rem' }}>
        <Flex vAlign='center' space='between'
            style={{ cursor: 'pointer' }}
            onMouseEnter={() => setVisible(true)}
            onMouseLeave={() => setVisible(false)}
        >
            <Flex vAlign='center' hAlign='start'
                onClick={() => setExpand(!expanded)}
            >
                {expanded ? <TriangleDownIcon /> : <TriangleEndIcon />}
                {props.title}
            </Flex>
            <Checkbox labelPosition='start'
                checked={props.items.every(e => e.checked) ? true : props.items.some(e => e.checked) ? 'mixed' : false}
                style={{ visibility: visible || props.items.some(e => e.checked) ? 'visible' : 'hidden' }}
                onClick={() => {
                    let items = [...props.items]
                    if (props.items.some(e => e.checked))
                        items.forEach(i => { i.checked = false; return i })
                    else if (!props.items.every(e => e.checked)) items.forEach(i => { i.checked = true; return i })
                    props.update(items)
                }} />
        </Flex>
        {expanded && props.items.map((item, index) => (
            <HoverCheckbox key={index}
                label={item.title}
                checked={item.checked}
                onClick={() => {
                    let items = [...props.items]
                    items[index] = { ...items[index], checked: !items[index].checked }
                    props.update(items)
                }}
            />
        ))}
    </div>
}

const HoverCheckbox = (props) => {
    const [visible, setVisible] = useState(false)

    return <Flex style={{ paddingLeft: '1rem', cursor: 'pointer' }}
        space='between'
        hAlign='center'
        vAlign='end'
        onClick={() => props.onClick()}
        onMouseEnter={() => setVisible(true)}
        onMouseLeave={() => setVisible(false)}
    >
        <Text content={props.label} />
        <Checkbox
            labelPosition='start'
            onClick={() => props.onClick}
            checked={props.checked}
            style={{ visibility: visible || props.checked ? 'visible' : 'hidden' }}
        />
    </Flex >
}

ReactDOM.render(<ReserveDashboard />, document.getElementById('root'))
