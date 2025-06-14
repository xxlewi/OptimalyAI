// Browser Inspector Tool - Universal browser inspection script
// This script can be injected into any page to collect comprehensive browser state

(function() {
    'use strict';
    
    window.BrowserInspector = {
        // Capture console logs
        consoleLogs: [],
        networkRequests: [],
        errors: [],
        
        init: function() {
            this.interceptConsole();
            this.interceptNetwork();
            this.interceptErrors();
            this.addInspectorPanel();
            console.log('🔍 Browser Inspector initialized');
        },
        
        interceptConsole: function() {
            const self = this;
            const methods = ['log', 'warn', 'error', 'info', 'debug'];
            
            methods.forEach(method => {
                const original = console[method];
                console[method] = function(...args) {
                    self.consoleLogs.push({
                        type: method,
                        message: args.map(arg => {
                            try {
                                return typeof arg === 'object' ? JSON.stringify(arg) : String(arg);
                            } catch (e) {
                                return String(arg);
                            }
                        }).join(' '),
                        timestamp: new Date().toISOString(),
                        stack: new Error().stack
                    });
                    
                    // Keep only last 100 logs
                    if (self.consoleLogs.length > 100) {
                        self.consoleLogs.shift();
                    }
                    
                    original.apply(console, args);
                };
            });
        },
        
        interceptNetwork: function() {
            const self = this;
            
            // Intercept fetch
            const originalFetch = window.fetch;
            window.fetch = function(...args) {
                const request = {
                    url: args[0],
                    method: args[1]?.method || 'GET',
                    timestamp: new Date().toISOString()
                };
                
                return originalFetch.apply(this, args).then(response => {
                    request.status = response.status;
                    request.statusText = response.statusText;
                    self.networkRequests.push(request);
                    
                    if (self.networkRequests.length > 50) {
                        self.networkRequests.shift();
                    }
                    
                    return response;
                });
            };
            
            // Intercept XMLHttpRequest
            const originalXHR = window.XMLHttpRequest;
            window.XMLHttpRequest = function() {
                const xhr = new originalXHR();
                const self = this;
                const request = {
                    timestamp: new Date().toISOString()
                };
                
                // Intercept open
                const originalOpen = xhr.open;
                xhr.open = function(method, url) {
                    request.method = method;
                    request.url = url;
                    originalOpen.apply(xhr, arguments);
                };
                
                // Intercept send
                const originalSend = xhr.send;
                xhr.send = function(data) {
                    request.data = data;
                    
                    xhr.addEventListener('load', function() {
                        request.status = xhr.status;
                        request.statusText = xhr.statusText;
                        request.response = xhr.responseText;
                        self.networkRequests.push(request);
                        
                        if (self.networkRequests.length > 50) {
                            self.networkRequests.shift();
                        }
                    });
                    
                    originalSend.apply(xhr, arguments);
                };
                
                return xhr;
            };
        },
        
        interceptErrors: function() {
            const self = this;
            
            window.addEventListener('error', function(event) {
                self.errors.push({
                    message: event.message,
                    source: event.filename,
                    line: event.lineno,
                    column: event.colno,
                    error: event.error ? event.error.stack : null,
                    timestamp: new Date().toISOString()
                });
                
                if (self.errors.length > 50) {
                    self.errors.shift();
                }
            });
            
            window.addEventListener('unhandledrejection', function(event) {
                self.errors.push({
                    message: 'Unhandled Promise Rejection',
                    reason: event.reason,
                    timestamp: new Date().toISOString()
                });
                
                if (self.errors.length > 50) {
                    self.errors.shift();
                }
            });
        },
        
        getPageState: function() {
            return {
                url: window.location.href,
                title: document.title,
                consoleLogs: this.consoleLogs,
                networkRequests: this.networkRequests,
                errors: this.errors,
                dom: this.getDOMSnapshot(),
                forms: this.getFormData(),
                localStorage: this.getLocalStorage(),
                sessionStorage: this.getSessionStorage(),
                cookies: document.cookie,
                viewport: {
                    width: window.innerWidth,
                    height: window.innerHeight,
                    scrollX: window.scrollX,
                    scrollY: window.scrollY
                },
                timestamp: new Date().toISOString()
            };
        },
        
        getDOMSnapshot: function() {
            const snapshot = {
                totalElements: document.querySelectorAll('*').length,
                forms: document.forms.length,
                images: document.images.length,
                links: document.links.length,
                scripts: document.scripts.length,
                stylesheets: document.styleSheets.length
            };
            
            // Get visible text content
            snapshot.visibleText = document.body.innerText.substring(0, 1000);
            
            // Get all input values
            snapshot.inputs = Array.from(document.querySelectorAll('input, select, textarea')).map(el => ({
                type: el.type || el.tagName.toLowerCase(),
                id: el.id,
                name: el.name,
                value: el.value,
                placeholder: el.placeholder,
                required: el.required,
                disabled: el.disabled,
                visible: el.offsetParent !== null
            }));
            
            return snapshot;
        },
        
        getFormData: function() {
            const forms = [];
            Array.from(document.forms).forEach((form, index) => {
                const formData = {
                    index: index,
                    id: form.id,
                    name: form.name,
                    action: form.action,
                    method: form.method,
                    fields: {}
                };
                
                Array.from(form.elements).forEach(element => {
                    if (element.name) {
                        formData.fields[element.name] = {
                            type: element.type,
                            value: element.value,
                            checked: element.checked,
                            selectedIndex: element.selectedIndex,
                            options: element.options ? Array.from(element.options).map(opt => ({
                                value: opt.value,
                                text: opt.text,
                                selected: opt.selected
                            })) : null
                        };
                    }
                });
                
                forms.push(formData);
            });
            return forms;
        },
        
        getLocalStorage: function() {
            const storage = {};
            try {
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    storage[key] = localStorage.getItem(key);
                }
            } catch (e) {
                storage.error = e.message;
            }
            return storage;
        },
        
        getSessionStorage: function() {
            const storage = {};
            try {
                for (let i = 0; i < sessionStorage.length; i++) {
                    const key = sessionStorage.key(i);
                    storage[key] = sessionStorage.getItem(key);
                }
            } catch (e) {
                storage.error = e.message;
            }
            return storage;
        },
        
        exportState: function() {
            const state = this.getPageState();
            const blob = new Blob([JSON.stringify(state, null, 2)], {type: 'application/json'});
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `browser-state-${Date.now()}.json`;
            a.click();
            URL.revokeObjectURL(url);
        },
        
        addInspectorPanel: function() {
            // Add a floating button to export state
            const button = document.createElement('div');
            button.innerHTML = '🔍';
            button.style.cssText = `
                position: fixed;
                bottom: 20px;
                right: 20px;
                width: 50px;
                height: 50px;
                background: #4CAF50;
                color: white;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 24px;
                cursor: pointer;
                z-index: 999999;
                box-shadow: 0 2px 10px rgba(0,0,0,0.3);
            `;
            
            button.onclick = () => {
                const state = this.getPageState();
                console.log('📋 Browser State:', state);
                
                // Copy to clipboard
                navigator.clipboard.writeText(JSON.stringify(state, null, 2)).then(() => {
                    alert('Browser state copied to clipboard!');
                }).catch(() => {
                    this.exportState();
                });
            };
            
            document.body.appendChild(button);
        }
    };
    
    // Auto-initialize
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => BrowserInspector.init());
    } else {
        BrowserInspector.init();
    }
})();