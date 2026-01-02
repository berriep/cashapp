/**
 * OPS Dashboard - React Flow Visualization
 * Phase 2: Live data from BAI database via API
 */

(function() {
    'use strict';

    console.log('OPS Dashboard script loaded - Phase 2');

    // Custom node component with bold title
    function CustomNode({ data }) {
        const lines = data.label.split('\n');
        const title = lines[0];
        const content = lines.slice(1).join('\n');
        const showLogo = data.showLogo;
        
        return React.createElement('div', { 
            style: { 
                fontSize: '12px',
                fontFamily: 'system-ui, -apple-system, sans-serif',
                color: '#1f2937',
                textAlign: 'left',
                whiteSpace: 'pre-line'
            }
        }, [
            React.createElement(ReactFlow.Handle, {
                key: 'target',
                type: 'target',
                position: 'left',
                style: { background: '#555' }
            }),
            showLogo ? React.createElement('div', {
                key: 'logo',
                style: {
                    display: 'flex',
                    alignItems: 'center',
                    marginBottom: '8px'
                }
            }, [
                React.createElement('img', {
                    key: 'logo-img',
                    src: '/bai/static/img/rabobank-logo.svg',
                    alt: 'Rabobank',
                    style: {
                        height: '20px',
                        marginRight: '8px'
                    }
                }),
                React.createElement('span', {
                    key: 'logo-title',
                    style: {
                        fontSize: '14px',
                        fontWeight: '700',
                        color: '#111827'
                    }
                }, title)
            ]) : React.createElement('div', { 
                key: 'title',
                style: { 
                    fontSize: '14px', 
                    fontWeight: '700', 
                    color: '#111827',
                    marginBottom: '8px'
                }
            }, title),
            React.createElement('div', { 
                key: 'content',
                style: { fontWeight: '400' }
            }, content),
            React.createElement(ReactFlow.Handle, {
                key: 'source',
                type: 'source',
                position: 'right',
                style: { background: '#555' }
            })
        ]);
    }

    function createNodesFromData(data) {
        const healthColors = {
            'green': '#10b981',
            'yellow': '#f59e0b',
            'red': '#ef4444',
            'grey': '#9ca3af'
        };
        
        const bankBorderColor = healthColors[data.bank_input.health];
        const cashappBorderColor = healthColors[data.cashapp_processing.health];
        const autobankBorderColor = healthColors[data.autobank_output.health];
        const globesBorderColor = healthColors[data.globes_output.health];
        
        return [
            {
                id: 'bank_rabo',
                type: 'custom',
                data: { 
                    label: `Rabobank\nTransactions: ${data.bank_input.tx_count}/${data.bank_input.expected}\nBalances: ${data.bank_input.bal_count}/${data.bank_input.expected}\nMatch: ${data.bank_input.match_count}/${data.bank_input.expected}\n\nLast Update: ${data.bank_input.last_update}`
                },
                position: { x: 50, y: 200 },
                sourcePosition: 'right',
                style: {
                    background: '#ffffff',
                    border: `2px solid ${bankBorderColor}`,
                    borderRadius: '4px',
                    padding: '16px 20px',
                    width: 240,
                    minHeight: '140px',
                    fontSize: '12px',
                    textAlign: 'left',
                    fontFamily: 'system-ui, -apple-system, sans-serif',
                    fontWeight: '400',
                    boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.08)',
                    whiteSpace: 'pre-line',
                    color: '#1f2937'
                }
            },
            {
                id: 'cashapp_bai',
                type: 'custom',
                data: { 
                    label: `BAI Database\nTransactions (Yesterday): ${data.cashapp_processing.total_day.toLocaleString()}\nStatus: ${data.cashapp_processing.status}\nLast Sync: ${data.cashapp_processing.last_sync}` 
                },
                position: { x: 380, y: 200 },
                sourcePosition: 'right',
                targetPosition: 'left',
                style: {
                    background: '#ffffff',
                    border: `2px solid ${cashappBorderColor}`,
                    borderRadius: '8px',
                    padding: '20px',
                    width: 240,
                    fontSize: '12px',
                    textAlign: 'left',
                    fontFamily: 'system-ui, -apple-system, sans-serif',
                    boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.08)',
                    whiteSpace: 'pre-line',
                    fontWeight: '400',
                    color: '#1f2937'
                }
            },
            {
                id: 'autobank',
                type: 'custom',
                data: { 
                    label: `Autobank\nRabobank: ${data.autobank_output.rabo_accounts}/${data.autobank_output.rabo_total}\nSuccess: ${data.autobank_output.success_data}\nNo Transactions: ${data.autobank_output.success_no_data}\nError: ${data.autobank_output.failed}\n\nStatus: ${data.autobank_output.status}` 
                },
                position: { x: 730, y: 80 },
                targetPosition: 'left',
                style: {
                    background: '#ffffff',
                    border: `2px solid ${autobankBorderColor}`,
                    borderRadius: '8px',
                    padding: '20px',
                    width: 220,
                    fontSize: '12px',
                    textAlign: 'left',
                    fontFamily: 'system-ui, -apple-system, sans-serif',
                    fontWeight: '400',
                    boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.08)',
                    whiteSpace: 'pre-line',
                    color: '#1f2937'
                }
            },
            {
                id: 'globes',
                type: 'custom',
                data: { 
                    label: `Globes\nRabobank: ${data.globes_output.rabo_accounts}/${data.globes_output.rabo_total}\n\nStatus: ${data.globes_output.status}` 
                },
                position: { x: 730, y: 270 },
                targetPosition: 'left',
                style: {
                    background: '#ffffff',
                    border: `2px solid ${globesBorderColor}`,
                    borderRadius: '8px',
                    padding: '20px',
                    width: 220,
                    fontSize: '12px',
                    textAlign: 'left',
                    fontFamily: 'system-ui, -apple-system, sans-serif',
                    fontWeight: '400',
                    boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.08)',
                    whiteSpace: 'pre-line',
                    color: '#1f2937'
                }
            }
        ];
    }

    function updateDatabaseStats(dbHealth) {
        if (!dbHealth) return;
        
        // Index hit rate with color
        const indexRate = dbHealth.index_hit_rate || 0;
        const indexRateEl = document.getElementById('stat-index-rate');
        if (indexRateEl) {
            indexRateEl.textContent = indexRate.toFixed(1) + '%';
            if (indexRate > 90) {
                indexRateEl.className = 'mb-0 text-success';
            } else if (indexRate > 70) {
                indexRateEl.className = 'mb-0 text-warning';
            } else {
                indexRateEl.className = 'mb-0 text-danger';
            }
        }
        
        // Cache hit rate with color
        const cacheRate = dbHealth.cache_hit_ratio || 0;
        const cacheRateEl = document.getElementById('stat-cache-rate');
        if (cacheRateEl) {
            cacheRateEl.textContent = cacheRate.toFixed(1) + '%';
            if (cacheRate > 95) {
                cacheRateEl.className = 'mb-0 text-success';
            } else if (cacheRate > 85) {
                cacheRateEl.className = 'mb-0 text-warning';
            } else {
                cacheRateEl.className = 'mb-0 text-danger';
            }
        }
        
        // Dead rows with color
        const deadRows = dbHealth.dead_rows || 0;
        const deadRowsEl = document.getElementById('stat-dead-rows');
        if (deadRowsEl) {
            deadRowsEl.textContent = deadRows.toLocaleString();
            if (deadRows < 1000) {
                deadRowsEl.className = 'mb-0 text-success';
            } else if (deadRows < 5000) {
                deadRowsEl.className = 'mb-0 text-warning';
            } else {
                deadRowsEl.className = 'mb-0 text-danger';
            }
        }
        
        // Partitions filled
        const filledPartitions = dbHealth.filled_partitions || 0;
        const totalPartitions = dbHealth.total_partitions || 0;
        const partitionsEl = document.getElementById('stat-partitions');
        if (partitionsEl) {
            partitionsEl.textContent = filledPartitions + '/' + totalPartitions;
            partitionsEl.className = 'mb-0';
        }
        
        // Active connections
        const activeConn = dbHealth.active_connections || 0;
        const totalConn = dbHealth.total_connections || 0;
        const connEl = document.getElementById('stat-connections');
        if (connEl) {
            connEl.textContent = activeConn + '/' + totalConn;
            connEl.className = 'mb-0';
        }
        
        // Database size
        const dbSizeEl = document.getElementById('stat-db-size');
        if (dbSizeEl) {
            dbSizeEl.textContent = dbHealth.db_size || 'N/A';
            dbSizeEl.className = 'mb-0';
        }
    }

    function createEdges(data) {
        const healthColors = {
            'green': '#10b981',
            'yellow': '#f59e0b',
            'red': '#ef4444',
            'grey': '#9ca3af'
        };
        
        return [
            {
                id: 'e1',
                source: 'bank_rabo',
                target: 'cashapp_bai',
                type: 'bezier',
                animated: false,
                style: { 
                    stroke: healthColors[data.bank_input.health],
                    strokeWidth: 2,
                    strokeDasharray: '5,5'
                }
            },
            {
                id: 'e2',
                source: 'cashapp_bai',
                target: 'autobank',
                type: 'bezier',
                animated: false,
                style: { 
                    stroke: healthColors[data.autobank_output.health],
                    strokeWidth: 2,
                    strokeDasharray: '5,5'
                }
            },
            {
                id: 'e3',
                source: 'cashapp_bai',
                target: 'globes',
                type: 'bezier',
                animated: false,
                style: { 
                    stroke: healthColors[data.globes_output.health],
                    strokeWidth: 2,
                    strokeDasharray: '5,5'
                }
            }
        ];
    }

    let isRefreshing = false;

    function initOpsFlow(isAutoRefresh = false) {
        if (isAutoRefresh && isRefreshing) {
            console.log('Refresh already in progress, skipping...');
            return;
        }
        
        console.log(isAutoRefresh ? 'Auto-refreshing OPS Flow...' : 'Initializing OPS Flow...');
        isRefreshing = true;
        
        const container = document.getElementById('ops-flow-container');
        if (!container) {
            console.error('Container not found');
            isRefreshing = false;
            return;
        }

        if (typeof ReactFlow === 'undefined') {
            container.innerHTML = '<div class="alert alert-danger m-3">React Flow library failed to load.</div>';
            isRefreshing = false;
            return;
        }

        // Only show spinner on initial load, not on refresh
        if (!isAutoRefresh) {
            container.innerHTML = '<div class="d-flex justify-content-center align-items-center" style="height: 100%;"><div class="spinner-border text-primary"></div></div>';
        }

        fetch('/bai/api/ops-status')
            .then(response => response.json())
            .then(data => {
                console.log('Received data:', data);
                
                const nodes = createNodesFromData(data);
                const edges = createEdges(data);
                
                const nodeTypes = {
                    custom: CustomNode
                };
                
                const flowElement = React.createElement(
                    ReactFlow.default, 
                    {
                        nodes: nodes,
                        edges: edges,
                        nodeTypes: nodeTypes,
                        fitView: !isAutoRefresh,  // Only fit view on initial load
                        minZoom: 0.5,
                        maxZoom: 1.5
                    }, 
                    [
                        React.createElement(ReactFlow.Controls, { key: 'controls' })
                    ]
                );

                ReactDOM.render(flowElement, container);
                console.log('OPS Dashboard ' + (isAutoRefresh ? 'refreshed' : 'initialized') + ' successfully');
                
                // Update database stats
                updateDatabaseStats(data.database_health);
                
                isRefreshing = false;
            })
            .catch(error => {
                console.error('Error:', error);
                container.innerHTML = '<div class="alert alert-danger m-3">Error loading data: ' + error.message + '</div>';
                isRefreshing = false;
            });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initOpsFlow(false));
    } else {
        initOpsFlow(false);
    }

    // Auto-refresh every 30 seconds
    setInterval(() => initOpsFlow(true), 30000);

    window.OPSDashboard = { reinit: initOpsFlow };

})();
