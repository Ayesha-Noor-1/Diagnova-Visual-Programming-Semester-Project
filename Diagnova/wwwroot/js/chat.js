(function () {
    var CHAT_POST_URL = '/Chat';
    var EMERGENCY_POST_URL = '/Chat?handler=EmergencyPlaces';
    var NEW_CHAT_POST_URL = '/Chat?handler=NewChat';

    function getAntiForgeryToken(root) {
        root = root || document;
        var input = root.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function escapeHtml(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function isDashboardEmbed() {
        return window.location.pathname.toLowerCase().indexOf('/dashboard') >= 0;
    }

    function getChatRoot() {
        return document.getElementById('chatMount') || document.querySelector('.dn-chat-app')?.closest('#chatMount') || document;
    }

    function scrollMessagesToBottom(root) {
        var messages = root.querySelector('#chatMessages');
        if (messages) messages.scrollTop = messages.scrollHeight;
    }

    function removeWelcome(root) {
        var welcome = root.querySelector('.dn-chat-welcome');
        if (welcome) welcome.remove();
    }

    function appendUserMessage(root, text) {
        var container = root.querySelector('#chatMessages');
        if (!container) return;
        removeWelcome(root);
        var row = document.createElement('div');
        row.className = 'dn-msg-row user dn-msg-pending';
        row.innerHTML =
            '<div class="dn-avatar user" aria-hidden="true"><span class="dn-avatar-letter">You</span></div>' +
            '<div class="dn-msg-bubble text-start">' +
            '<div class="dn-msg-meta">You</div>' +
            '<div class="dn-chat-content">' + escapeHtml(text) + '</div>' +
            '</div>';
        container.appendChild(row);
        scrollMessagesToBottom(root);
    }

    function showTypingIndicator(root) {
        var container = root.querySelector('#chatMessages');
        if (!container || container.querySelector('#chatTypingIndicator')) return;
        var row = document.createElement('div');
        row.id = 'chatTypingIndicator';
        row.className = 'dn-msg-row';
        row.innerHTML =
            '<div class="dn-avatar bot" aria-hidden="true"><span class="dn-avatar-letter">AI</span></div>' +
            '<div class="dn-msg-bubble"><div class="dn-msg-meta">Diagnova</div>' +
            '<div class="dn-typing"><span></span><span></span><span></span></div></div>';
        container.appendChild(row);
        scrollMessagesToBottom(root);
    }

    function removeTypingIndicator(root) {
        var el = root.querySelector('#chatTypingIndicator');
        if (el) el.remove();
    }

    function initChatForm(root) {
        root = root || document;
        var form = root.querySelector('#chatForm');
        var ta = root.querySelector('#UserInput');
        if (!form || !ta) return;
        if (form.dataset.wired === '1') return;
        form.dataset.wired = '1';

        ta.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 140) + 'px';
        });

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            submitChatForm(root, form);
        });

        scrollMessagesToBottom(root);
    }

    function submitChatForm(root, form) {
        var ta = root.querySelector('#UserInput');
        var btn = root.querySelector('#chatSendBtn');
        var text = (ta && ta.value ? ta.value : '').trim();
        if (!text) return;

        if (ta) {
            ta.value = '';
            ta.style.height = 'auto';
        }

        appendUserMessage(root, text);
        showTypingIndicator(root);
        if (btn) btn.disabled = true;

        var token = getAntiForgeryToken(root);
        var fd = new FormData(form);
        fd.set('UserInput', text);

        fetch(CHAT_POST_URL, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'X-Requested-With': isDashboardEmbed() ? 'DiagnovaDashboard' : '',
                'RequestVerificationToken': token
            },
            body: fd
        })
            .then(function (r) { return r.text(); })
            .then(function (html) {
                removeTypingIndicator(root);
                replaceChatMount(html);
            })
            .catch(function () {
                removeTypingIndicator(root);
                alert('Could not send message. Please try again.');
            })
            .finally(function () {
                if (btn) btn.disabled = false;
                var ta2 = root.querySelector('#UserInput');
                if (ta2) {
                    ta2.value = '';
                    ta2.style.height = 'auto';
                    ta2.focus();
                }
            });
    }

    function replaceChatMount(html) {
        var mount = document.getElementById('chatMount');
        if (mount) {
            mount.innerHTML = html;
            reinitChat(mount);
            return;
        }
        if (html.indexOf('dn-chat-app') >= 0 && isDashboardEmbed()) {
            var contentDiv = document.getElementById('dynamicContent');
            if (contentDiv) {
                contentDiv.innerHTML = '<div class="dn-dashboard-chat-wrap" id="chatMount">' + html + '</div>';
                reinitChat(document.getElementById('chatMount'));
            }
        }
    }

    function reinitChat(mount) {
        if (!mount) return;
        window.DiagnovaChat.init(mount);
        window.DiagnovaChat.initEmergencyMap(mount);
        window.DiagnovaChat.wireNewChatForm(mount);
        window.DiagnovaChat.wireEmergencyBanner(mount);

        var isEmergency = mount.querySelector('[data-show-emergency="true"]') ||
            mount.querySelector('#emergencyModal') ||
            mount.querySelector('#chatEmergencyBanner');
        if (isEmergency) {
            document.documentElement.setAttribute('data-show-emergency', '1');
            var emModal = document.getElementById('emergencyModal') || mount.querySelector('#emergencyModal');
            if (emModal) ensureEmergencyModalOnBody(emModal);
            setTimeout(function () { showEmergencyModal(mount); }, 150);
        }
        scrollMessagesToBottom(mount);
    }

    function wireEmergencyBanner(root) {
        root = root || document;
        var btn = root.querySelector('#openEmergencyMapBtn');
        if (!btn || btn.dataset.wired === '1') return;
        btn.dataset.wired = '1';
        btn.addEventListener('click', function () {
            showEmergencyModal(root);
        });
    }

    function wireNewChatForm(root) {
        root = root || document;
        var form = root.querySelector('#newChatForm, [data-dn-new-chat]');
        if (!form || form.dataset.wired === '1') return;
        form.dataset.wired = '1';
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            startNewChat(root);
        });
    }

    function startNewChat(root) {
        root = root || document;
        var token = getAntiForgeryToken(root);
        var body = new URLSearchParams();
        body.set('__RequestVerificationToken', token);

        fetch(NEW_CHAT_POST_URL, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': isDashboardEmbed() ? 'DiagnovaDashboard' : '',
                'RequestVerificationToken': token
            },
            body: body.toString()
        })
            .then(function (r) {
                if (isDashboardEmbed()) return r.text();
                window.location.href = '/Chat';
                return null;
            })
            .then(function (html) {
                if (html) {
                    document.documentElement.removeAttribute('data-show-emergency');
                    replaceChatMount(html);
                }
            })
            .catch(function () {
                if (isDashboardEmbed()) loadFreshPartial();
                else window.location.href = '/Chat';
            });
    }

    function initEmergencyMap(root) {
        root = root || document;
        var modal = document.getElementById('emergencyModal') || root.querySelector('#emergencyModal');
        if (modal) ensureEmergencyModalOnBody(modal);
        var scope = modal || root;
        var btn = scope.querySelector('#emergencyShareLocationBtn');
        var statusEl = scope.querySelector('#emergencyGeoStatus');
        var mapEl = scope.querySelector('#emergencyMap');
        var listEl = scope.querySelector('#emergencyFacilityList');
        if (!btn || !mapEl || !listEl) return;
        if (btn.dataset.wired === '1') return;
        btn.dataset.wired = '1';

        var mapInstance = null;
        var layerGroup = null;

        function loadScript(src) {
            return new Promise(function (resolve, reject) {
                if (document.querySelector('script[src="' + src + '"]')) { resolve(); return; }
                var s = document.createElement('script');
                s.src = src;
                s.async = true;
                s.onload = resolve;
                s.onerror = function () { reject(new Error('Could not load: ' + src)); };
                document.head.appendChild(s);
            });
        }

        function loadLeaflet() {
            if (window.L) return Promise.resolve();
            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            if (!document.querySelector('link[href*="leaflet"]')) document.head.appendChild(link);
            return loadScript('https://unpkg.com/leaflet@1.9.4/dist/leaflet.js');
        }

        function ensureMap(lat, lng) {
            return loadLeaflet().then(function () {
                if (!mapInstance) {
                    mapEl.innerHTML = '';
                    mapInstance = L.map(mapEl).setView([lat, lng], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        maxZoom: 19,
                        attribution: '&copy; OpenStreetMap'
                    }).addTo(mapInstance);
                    layerGroup = L.featureGroup().addTo(mapInstance);
                } else {
                    mapInstance.setView([lat, lng], 13);
                    layerGroup.clearLayers();
                }
            });
        }

        function buildGoogleMapsSearchUrl(name, address) {
            var parts = [];
            if (name) parts.push(name);
            if (address) parts.push(address);
            var query = parts.join(', ').trim();
            if (!query) return 'https://www.google.com/maps';
            return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(query);
        }

        function renderFacilities(data) {
            listEl.innerHTML = '';
            if (!data || !data.facilities || !data.facilities.length) {
                listEl.innerHTML = '<p class="text-danger mb-0"><strong>No facilities found nearby.</strong> Call <strong>1122</strong> immediately.</p>';
                return;
            }
            var grid = document.createElement('div');
            grid.className = 'dn-facility-cards';
            data.facilities.forEach(function (f) {
                var dist = f.distanceKm != null ? f.distanceKm : f.DistanceKm;
                var name = f.name || f.Name || 'Hospital';
                var addr = f.address || f.Address || '';
                var mapsUrl = buildGoogleMapsSearchUrl(name, addr);
                var card = document.createElement('article');
                card.className = 'dn-facility-card';
                card.innerHTML =
                    '<div class="dn-facility-card-head">' +
                    '<h6 class="dn-facility-name">' + escapeHtml(name) + '</h6>' +
                    '<span class="dn-facility-distance">' + escapeHtml(String(dist)) + ' km</span>' +
                    '</div>' +
                    (addr ? '<p class="dn-facility-address">' + escapeHtml(addr) + '</p>' : '') +
                    '<a href="' + mapsUrl + '" target="_blank" rel="noopener" class="btn btn-sm btn-outline-danger dn-facility-maps-btn">' +
                    'Open in Google Maps</a>';
                grid.appendChild(card);
            });
            listEl.appendChild(grid);
        }

        function requestLocation() {
            if (!navigator.geolocation) {
                if (statusEl) statusEl.textContent = 'Geolocation is not supported.';
                return;
            }
            if (statusEl) statusEl.textContent = 'Requesting your location…';
            btn.disabled = true;

            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    var lat = pos.coords.latitude;
                    var lng = pos.coords.longitude;
                    var token = getAntiForgeryToken(root);
                    var sessionInput = root.querySelector('input[name="sessionId"]');
                    var body = new URLSearchParams();
                    body.set('lat', String(lat));
                    body.set('lng', String(lng));
                    body.set('__RequestVerificationToken', token);
                    if (sessionInput && sessionInput.value) body.set('sessionId', sessionInput.value);

                    fetch(EMERGENCY_POST_URL, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded',
                            'RequestVerificationToken': token
                        },
                        body: body.toString()
                    })
                        .then(function (r) {
                            return r.text().then(function (text) {
                                if (!r.ok) throw new Error('Request failed (' + r.status + ')');
                                if ((r.headers.get('content-type') || '').toLowerCase().indexOf('json') < 0)
                                    throw new Error('Invalid response — refresh and try again.');
                                return JSON.parse(text);
                            });
                        })
                        .then(function (data) {
                            if (!data || !data.ok) {
                                if (statusEl) statusEl.textContent = (data && data.error) || 'Could not load hospitals.';
                                btn.disabled = false;
                                return;
                            }
                            renderFacilities(data);
                            var pt = data.patient || data.Patient;
                            var plat = pt.lat != null ? pt.lat : pt.Lat;
                            var plng = pt.lng != null ? pt.lng : pt.Lng;
                            return ensureMap(plat, plng).then(function () {
                                L.circleMarker([plat, plng], { radius: 9, color: '#b02a37', fillColor: '#dc3545', fillOpacity: 0.9 })
                                    .addTo(layerGroup).bindPopup('You are here');
                                (data.facilities || []).forEach(function (f) {
                                    var la = f.latitude != null ? f.latitude : f.Latitude;
                                    var lo = f.longitude != null ? f.longitude : f.Longitude;
                                    if (la == null || lo == null) return;
                                    L.marker([la, lo]).addTo(layerGroup).bindPopup(f.name || f.Name);
                                });
                                if (layerGroup.getBounds().isValid()) mapInstance.fitBounds(layerGroup.getBounds().pad(0.2));
                                setTimeout(function () { mapInstance.invalidateSize(); }, 400);
                                if (statusEl) statusEl.textContent = 'Nearest facilities shown on map.';
                                btn.disabled = false;
                            });
                        })
                        .catch(function (err) {
                            if (statusEl) statusEl.textContent = (err && err.message) ? err.message : 'Could not load map.';
                            btn.disabled = false;
                        });
                },
                function () {
                    if (statusEl) statusEl.textContent = 'Location denied — enable location or search maps manually.';
                    btn.disabled = false;
                },
                { enableHighAccuracy: true, timeout: 20000, maximumAge: 60000 }
            );
        }

        btn.addEventListener('click', requestLocation);

        if (modal && window.bootstrap) {
            modal.addEventListener('shown.bs.modal', function () {
                setTimeout(function () { if (mapInstance) mapInstance.invalidateSize(); }, 350);
                if (modal.getAttribute('data-auto-locate') === 'true' && (!statusEl || !statusEl.textContent))
                    requestLocation();
            });
        }
    }

    function ensureEmergencyModalOnBody(modal) {
        if (!modal || modal.parentElement === document.body) return;
        document.body.appendChild(modal);
    }

    function showEmergencyModal(root) {
        root = root || document;
        var modal = document.getElementById('emergencyModal') || root.querySelector('#emergencyModal');
        if (modal && window.bootstrap) {
            ensureEmergencyModalOnBody(modal);
            modal.setAttribute('data-auto-locate', 'true');
            bootstrap.Modal.getOrCreateInstance(modal).show();
        }
    }

    function loadFreshPartial() {
        fetch('/Chat?handler=Partial', { credentials: 'same-origin' })
            .then(function (r) { return r.text(); })
            .then(replaceChatMount);
    }

    window.DiagnovaChat = {
        init: initChatForm,
        initEmergencyMap: initEmergencyMap,
        wireNewChatForm: wireNewChatForm,
        wireEmergencyBanner: wireEmergencyBanner,
        startNewChat: startNewChat,
        showEmergencyModal: showEmergencyModal,
        submitChatForm: submitChatForm,
        loadFreshPartial: loadFreshPartial
    };

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.querySelector('.dn-chat-app') ? document : getChatRoot();
        initChatForm(root);
        wireNewChatForm(root);
        initEmergencyMap(root);
        wireEmergencyBanner(root);
        if (document.documentElement.getAttribute('data-show-emergency') === '1')
            showEmergencyModal(root);
    });
})();

