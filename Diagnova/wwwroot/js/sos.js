(function () {
    'use strict';

    var SOS_API = '/api/sos/trigger';

    function escapeHtml(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function buildGoogleMapsSearchUrl(name, address) {
        var parts = [];
        if (name) parts.push(name);
        if (address) parts.push(address);
        var query = parts.join(', ').trim();
        if (!query) return 'https://www.google.com/maps';
        return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(query);
    }

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

    function setStatus(text, isError) {
        var el = document.getElementById('sosStatus');
        if (!el) return;
        el.textContent = text;
        el.className = 'dn-sos-status small mb-3' + (isError ? ' text-danger fw-semibold' : ' text-muted');
    }

    function renderFacilities(facilities, lat, lng) {
        var listEl = document.getElementById('sosFacilityList');
        if (!listEl) return;
        listEl.innerHTML = '';
        if (!facilities || !facilities.length) {
            var mapsFallback = lat != null && lng != null
                ? 'https://www.google.com/maps/search/hospitals/@' + lat + ',' + lng + ',14z'
                : 'https://www.google.com/maps/search/hospitals+near+me';
            listEl.innerHTML =
                '<p class="text-danger mb-2"><strong>No hospitals found in OpenStreetMap here.</strong> Call <strong>1122</strong>.</p>' +
                '<a href="' + mapsFallback + '" target="_blank" rel="noopener" class="btn btn-sm btn-danger">Search hospitals on Google Maps</a>';
            return;
        }
        var grid = document.createElement('div');
        grid.className = 'dn-facility-cards';
        facilities.forEach(function (f) {
            var dist = f.distanceKm != null ? f.distanceKm : f.DistanceKm;
            var name = f.name || f.Name || 'Hospital';
            var addr = f.address || f.Address || '';
            var notes = f.notes || f.Notes || '';
            var mapsUrl = notes.indexOf('https://www.google.com/maps') === 0
                ? notes
                : buildGoogleMapsSearchUrl(name, addr);
            var card = document.createElement('article');
            card.className = 'dn-facility-card';
            card.innerHTML =
                '<div class="dn-facility-card-head">' +
                '<h6 class="dn-facility-name">' + escapeHtml(name) + '</h6>' +
                (dist != null && dist > 0 ? '<span class="dn-facility-distance">' + escapeHtml(String(dist)) + ' km</span>' : '') +
                '</div>' +
                (addr ? '<p class="dn-facility-address">' + escapeHtml(addr) + '</p>' : '') +
                '<a href="' + mapsUrl + '" target="_blank" rel="noopener" class="btn btn-sm btn-outline-danger dn-facility-maps-btn">Open in Google Maps</a>';
            grid.appendChild(card);
        });
        listEl.appendChild(grid);
    }

    var mapInstance = null;
    var layerGroup = null;

    function renderMap(lat, lng, facilities) {
        var mapEl = document.getElementById('sosMap');
        if (!mapEl) return Promise.resolve();

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

            L.circleMarker([lat, lng], { radius: 9, color: '#b02a37', fillColor: '#dc3545', fillOpacity: 0.9 })
                .addTo(layerGroup).bindPopup('You are here');

            (facilities || []).forEach(function (f) {
                var la = f.latitude != null ? f.latitude : f.Latitude;
                var lo = f.longitude != null ? f.longitude : f.Longitude;
                if (la == null || lo == null) return;
                L.marker([la, lo]).addTo(layerGroup).bindPopup(f.name || f.Name);
            });

            if (layerGroup.getBounds().isValid()) mapInstance.fitBounds(layerGroup.getBounds().pad(0.2));
            setTimeout(function () { mapInstance.invalidateSize(); }, 400);
        });
    }

    function ensureBootstrap() {
        if (window.bootstrap) return Promise.resolve();
        return loadScript('/lib/bootstrap/dist/js/bootstrap.bundle.min.js');
    }

    function showResultModal() {
        return ensureBootstrap().then(function () {
            var modal = document.getElementById('sosModal');
            if (modal && window.bootstrap) {
                if (modal.parentElement !== document.body) document.body.appendChild(modal);
                bootstrap.Modal.getOrCreateInstance(modal).show();
            }
        });
    }

    function applyResponse(data, lat, lng) {
        var actions = document.getElementById('sosActions');
        if (actions) actions.classList.remove('d-none');

        var mapsLink = document.getElementById('sosMapsLink');
        var url = data.locationMapsUrl || data.LocationMapsUrl;
        if (mapsLink && url) {
            mapsLink.href = url;
            mapsLink.classList.remove('d-none');
        }

        var smsLink = document.getElementById('sosSmsLink');
        var smsUrl = data.smsUrl || data.SmsUrl;
        if (smsLink && smsUrl) {
            smsLink.href = smsUrl;
            smsLink.classList.remove('d-none');
        }

        var profileEl = document.getElementById('sosProfilePreview');
        var profile = data.condensedProfile || data.CondensedProfile;
        if (profileEl && profile) profileEl.textContent = profile;

        var facilities = data.facilities || data.Facilities || [];
        renderFacilities(facilities, lat, lng);

        if (lat != null && lng != null) {
            renderMap(lat, lng, facilities);
        }

        if (data.emailSent || data.EmailSent) {
            setStatus(data.message || data.Message || 'SOS email sent to your emergency contact.', false);
        } else {
            setStatus(data.error || data.Error || 'SOS could not be sent. Check your emergency contact email in profile.', true);
        }
    }

    function triggerSos(lat, lng) {
        setStatus('Sending SOS alert with your location and medical profile…', false);
        showResultModal().catch(function () { /* modal optional */ });

        return fetch(SOS_API, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                latitude: lat,
                longitude: lng,
                note: 'One-tap SOS — user needs immediate help.'
            })
        })
            .then(function (r) {
                return r.json().then(function (data) {
                    if (!r.ok) throw data;
                    return data;
                });
            })
            .then(function (data) {
                applyResponse(data, lat, lng);
            })
            .catch(function (err) {
                var msg = (err && (err.error || err.Error)) || 'SOS failed. Try again or call 1122.';
                setStatus(msg, true);
                showResultModal();
            });
    }

    function onSosFabClick() {
        if (!window.confirm('Send SOS now?\n\nYour GPS location and medical profile will be emailed to your emergency contact immediately.')) {
            return;
        }

        setStatus('Getting your location…', false);

        if (!navigator.geolocation) {
            setStatus('Geolocation is not supported. SOS will send without location.', true);
            triggerSos(null, null);
            return;
        }

        navigator.geolocation.getCurrentPosition(
            function (pos) {
                triggerSos(pos.coords.latitude, pos.coords.longitude);
            },
            function () {
                setStatus('Location denied — sending SOS without GPS. Add location manually via SMS if available.', true);
                triggerSos(null, null);
            },
            { enableHighAccuracy: true, timeout: 20000, maximumAge: 30000 }
        );
    }

    function init() {
        var fab = document.getElementById('dnSosFab');
        if (!fab || fab.dataset.wired === '1') return;
        fab.dataset.wired = '1';
        fab.addEventListener('click', onSosFabClick);

        var modal = document.getElementById('sosModal');
        if (modal && window.bootstrap) {
            modal.addEventListener('shown.bs.modal', function () {
                if (mapInstance) setTimeout(function () { mapInstance.invalidateSize(); }, 350);
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.DiagnovaSos = { trigger: triggerSos, init: init };
})();
