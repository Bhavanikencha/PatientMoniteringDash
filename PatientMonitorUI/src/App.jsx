

import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import MonitorCanvas from './MonitorCanvas';
import PatientCard from './PatientCard'; 
import './App.css';

// --- CONFIG ---
const API_URL = "http://localhost:5191";

function App() {
  const [patients, setPatients] = useState([]);
  const [alerts, setAlerts] = useState([]);
  
  // Data Stores
  const [dashboardVitals, setDashboardVitals] = useState({});
  const [waveforms, setWaveforms] = useState({});

  // UI State
  const [view, setView] = useState('monitor'); // monitor, details, alerts
  const [activePatient, setActivePatient] = useState(null);
  const [connection, setConnection] = useState(null);
  const [monitorQuery, setMonitorQuery] = useState('');
  const [detailsQuery, setDetailsQuery] = useState('');

  // Audio Logic
  const audioRef = useRef(new Audio("https://actions.google.com/sounds/v1/alarms/beep_short.ogg"));
  const audioEnabled = useRef(false);

  // --- HELPERS ---
  // Safely get case-insensitive property
  const getVal = (obj, key) => {
      if (!obj) return '--';
      return obj[key] ?? obj[key.toLowerCase()] ?? obj[key.toUpperCase()] ?? '--';
  };

  const getGridData = (pid) => dashboardVitals[pid] || {};
  
  // --- FIX: RESTORED MISSING HELPER ---
  const getWaveData = (pid) => waveforms[pid] || {};

  // 1. INITIAL FETCH
   useEffect(() => {
    const fetchData = async () => {
        try {
            const pRes = await axios.get(`${API_URL}/api/dashboard/patients`);
            setPatients(pRes.data);
            const aRes = await axios.get(`${API_URL}/api/dashboard/alerts`);
            setAlerts(aRes.data);
        } catch (err) {
            console.error("Initialization Failed:", err);
        }
    };
    fetchData();

    // --- FIX: PROPERLY UNLOCK AUDIO ---
    const unlockAudio = () => {
        audioEnabled.current = true;
        // We must actually try to play the sound once on click, then pause it.
        // This tells the browser "The user allows this sound."
        audioRef.current.play()
            .then(() => {
                audioRef.current.pause();
                audioRef.current.currentTime = 0;
                console.log("Audio Engine Unlocked 🔊");
            })
            .catch(err => console.error("Audio Unlock Failed:", err));
    };

    // Listen for the first click anywhere on the page
    window.addEventListener('click', unlockAudio, { once: true });
    
    return () => window.removeEventListener('click', unlockAudio);
  }, []);

  // 2. SIGNALR SETUP (With Strict Mode Safety)
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/VitalsHub`)
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    let isMounted = true;

    conn.start()
      .then(() => {
        if (isMounted) {
            console.log("SignalR Connected");
            setConnection(conn);
        } else {
            // If component unmounted during connection, stop it immediately
            conn.stop();
        }
      })
      .catch(e => console.error('Connection failed: ', e));

    // Listeners
    conn.on('ReceiveDashboardUpdate', (data) => {
      const pid = data.patientId || data.PatientId;
      setDashboardVitals(prev => ({ ...prev, [pid]: data }));

      const status = data.status || data.Status;
      if(status === "Critical" && audioEnabled.current) {
         audioRef.current.play().catch(()=>{});
      }
    });

 conn.on('ReceiveDashboardUpdate', (data) => {
      const pid = data.patientId || data.PatientId;
      setDashboardVitals(prev => ({ ...prev, [pid]: data }));

      const status = data.status || data.Status;
      
      // --- FIX: BETTER PLAYBACK LOGIC ---
      if (status === "Critical" && audioEnabled.current) {
         // Reset time so it plays from start even if triggered rapidly
         audioRef.current.currentTime = 0; 
         audioRef.current.play()
             .catch(e => console.warn("Audio blocked or failed:", e));
      }
    });

    // --- WAVEFORM / FETAL UPDATES ---
    // Server may emit waveform or fetal-specific events; listen for common names
    const handleWaveUpdate = (data) => {
      if (!data) return;
      const pid = data.patientId || data.PatientId || data.pid;
      if (!pid) return;
      setWaveforms(prev => ({ ...prev, [pid]: { ...(prev[pid] || {}), ...data } }));
    };

    conn.on('ReceiveWaveform', handleWaveUpdate);
    conn.on('ReceiveWaveUpdate', handleWaveUpdate);
    conn.on('ReceiveFetalUpdate', handleWaveUpdate);
    conn.on('ReceiveFetal', handleWaveUpdate);


    // Clean up on unmount
    return () => {
        isMounted = false;
        conn.stop();
    };
  }, []);

  // 3. ACTIONS
  const handleOpenMonitor = async (patient) => {
    setActivePatient(patient);
    // Wait a brief moment if connection is still initializing
    if(connection && connection.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke("JoinPatientMonitor", patient.patientId);
      } catch (err) { console.error("Join Failed", err); }
    }
  };

  const handleCloseMonitor = async () => {
    if (activePatient && connection && connection.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke("LeavePatientMonitor", activePatient.patientId);
      } catch (err) { console.error("Leave Failed", err); }
    }
    setActivePatient(null);
    setWaveforms({}); 
  };


  
// ... inside App component ...
  
  // PRE-CALCULATE MONITOR CLASSES
  // We calculate this here so the JSX stays clean and doesn't crash
  let monitorClasses = { hr: '', spo2: '', temp: '', fetal: '', overlay: 'monitor-overlay' };
  let isCrit = false, isWarn = false;

  if (activePatient) {
      const v = getGridData(activePatient.patientId);
      const status = getVal(v, 'Status');
      isCrit = status === "Critical";
      isWarn = status === "Warning";

      // 1. Overlay Border Class
      if (isCrit) monitorClasses.overlay += " critical";
      else if (isWarn) monitorClasses.overlay += " warning";

      // 2. Individual Box Classes
      monitorClasses.hr = checkVitalStatus('HR', getVal(v, 'ECG'));
      monitorClasses.spo2 = checkVitalStatus('SPO2', getVal(v, 'SpO2'));
      monitorClasses.temp = checkVitalStatus('TEMP', getVal(v, 'Temp'));
      
      // Check both streams for Fetal Data
      const waveData = getWaveData(activePatient.patientId) || {}; 
      const fetalVal = getVal(waveData, 'FetalHR');
      monitorClasses.fetal = checkVitalStatus('FETAL', fetalVal);
  }


  return (
    <div className="app-container">
      {/* SIDEBAR */}
      <div className="sidebar">
        <div className="brand">VITAL<span style={{color:'var(--neon-green)'}}>TRACK</span></div>
        <button className={`nav-btn ${view==='monitor'?'active':''}`} onClick={()=>setView('monitor')}>LIVE MONITOR</button>
        <button className={`nav-btn ${view==='details'?'active':''}`} onClick={()=>setView('details')}>PATIENT RECORDS</button>
        <button className={`nav-btn ${view==='alerts'?'active':''}`} onClick={()=>setView('alerts')}>ALERT LOGS</button>
      </div>

      {/* MAIN CONTENT */}
      <div className="main-content">
        
        {/* VIEW 1: LIVE MONITOR (Grid) */}
        {view === 'monitor' && (
          <div>
            <h2 style={{borderBottom:'1px solid #333', paddingBottom:'15px', marginBottom:'20px'}}>ICU Ward Overview</h2>
            <div style={{display:'flex', justifyContent:'space-between', alignItems:'center', marginBottom:20}}>
              <div style={{color:'#999', fontSize:13}}>Showing {patients.length} patients</div>
              <div className="search-container">
                <input
                  className="search-input"
                  placeholder="Search patients by name or ID..."
                  value={monitorQuery}
                  onChange={e => setMonitorQuery(e.target.value)}
                />
              </div>
            </div>
            <div className="flex-container">
                {patients.length === 0 && <div style={{color:'#666', fontStyle:'italic'}}>Loading patients...</div>}

                {patients
                  .filter(p => {
                    if (!monitorQuery) return true;
                    const q = monitorQuery.toString().toLowerCase();
                    return (p.name || '').toLowerCase().includes(q)
                        || (p.patientId || '').toString().toLowerCase().includes(q)
                        || (p.doctorName || '').toLowerCase().includes(q);
                  })
                  .map(p => (
                    <PatientCard
                      key={p.patientId}
                      patient={p}
                      vitals={getGridData(p.patientId)}
                      onMonitor={handleOpenMonitor}
                    />
                  ))}
            </div>
          </div>
        )}

        {/* VIEW 2: DETAILS */}
        
{/* VIEW 2: DETAILS (UPDATED WITH ALL FIELDS) */}
        {view === 'details' && (
           <div style={{ width: '100%' }}> {/* Ensure parent is full width */}
             <h2 style={{borderBottom:'1px solid #333', paddingBottom:'15px', marginBottom:'20px'}}>Medical Database</h2>
             <div style={{display:'flex', justifyContent:'space-between', alignItems:'center', marginBottom:20}}>
               <div style={{color:'#999', fontSize:13}}>Records: {patients.length}</div>
               <div className="search-container">
                 <input
                   className="search-input"
                   placeholder="Search records by name, ID, doctor..."
                   value={detailsQuery}
                   onChange={e => setDetailsQuery(e.target.value)}
                 />
               </div>
             </div>

             {patients
               .filter(p => {
                 if (!detailsQuery) return true;
                 const q = detailsQuery.toLowerCase();
                 return (p.name || '').toLowerCase().includes(q)
                     || (p.patientId || '').toString().toLowerCase().includes(q)
                     || (p.doctorName || '').toLowerCase().includes(q)
                     || (p.mobile || '').toLowerCase().includes(q);
               })
               .map(p => (
               <div key={p.patientId} className="detail-card">
                 
                 {/* LEFT SIDEBAR: ID & Photo */}
                 <div className="dc-sidebar">
                   <div className="dc-photo">👤</div>
                   <div style={{color:'var(--neon-green)', fontFamily:'monospace', fontSize:'16px', fontWeight:'bold'}}>
                     #{p.patientId}
                   </div>
                   <div style={{fontSize:'11px', color:'#666', marginTop:'5px'}}>
                     {p.isPregnant ? "PREGNANCY WARD" : "GENERAL ICU"}
                   </div>
                 </div>

                 {/* RIGHT CONTENT: All Data */}
                 <div className="dc-content">
                   
                   {/* SECTION 1: PERSONAL & CONTACT */}
                   <div>
                     <div className="dc-section-title">Personal Information</div>
                     <div className="dc-grid">
                        <div><span className="info-label">Full Name</span><span className="info-val">{p.name}</span></div>
                        <div><span className="info-label">Date of Birth</span><span className="info-val">{p.dateOfBirth ? new Date(p.dateOfBirth).toLocaleDateString() : '--'}</span></div>
                        <div><span className="info-label">Blood Type</span><span className="info-val" style={{color:'var(--neon-red)', fontWeight:'bold'}}>{p.bloodType || 'Unknown'}</span></div>
                        <div><span className="info-label">Mobile</span><span className="info-val">{p.mobile || '--'}</span></div>
                        <div><span className="info-label">Attending Doctor</span><span className="info-val">{p.doctorName}</span></div>
                        <div className="dc-full-width"><span className="info-label">Address</span><span className="info-val">{p.address || 'No address on file'}</span></div>
                     </div>
                   </div>

                   {/* SECTION 2: CLINICAL HISTORY */}
                   <div>
                     <div className="dc-section-title">Medical History</div>
                     <div className="dc-grid">
                        
                        {/* Conditions */}
                        <div className="dc-full-width">
                            <span className="info-label">Diagnosed Conditions</span>
                            <span className="info-val" style={{color:'var(--neon-blue)'}}>
                                {p.conditions && p.conditions.length > 0 
                                  ? p.conditions.map(c => c.diagnosis).join(', ') 
                                  : "None Reported"}
                            </span>
                        </div>

                        {/* Medications */}
                        <div>
                            <span className="info-label">Current Medications</span>
                            {p.medications && p.medications.length > 0 ? (
                                <ul className="info-list">
                                    {p.medications.map((m, idx) => (
                                        <li key={idx}>{m.drugName} <span style={{color:'#666'}}>({m.dosage})</span></li>
                                    ))}
                                </ul>
                            ) : <span className="info-val">None</span>}
                        </div>

                        {/* Surgeries */}
                        <div>
                            <span className="info-label">Surgical History</span>
                            {p.surgeries && p.surgeries.length > 0 ? (
                                <ul className="info-list">
                                    {p.surgeries.map((s, idx) => (
                                        <li key={idx}>{s.procedureName} <span style={{color:'#666'}}>({s.year})</span></li>
                                    ))}
                                </ul>
                            ) : <span className="info-val">None</span>}
                        </div>

                        {/* Family History */}
                        <div className="dc-full-width">
                            <span className="info-label">Family Medical History</span>
                            <span className="info-val">{p.familyHistory || "No significant history recorded."}</span>
                        </div>

                     </div>
                   </div>

                 </div>
               </div>
             ))}
          </div>
        )}



        {/* VIEW 3: ALERTS */}
        {view === 'alerts' && (
          <div>
            <h2 style={{borderBottom:'1px solid #333', paddingBottom:'15px', marginBottom:'20px'}}>System Alert Logs</h2>
            {alerts.map((a, i) => (
              <div key={i} className="alert-item">
                <div>
                    <div style={{color: a.severity === 'Critical' ? '#ff2a2a' : '#ffa500', fontWeight:'bold', marginBottom:'4px'}}>
                        {a.severity?.toUpperCase()}
                    </div>
                    <div style={{color:'#ccc'}}>{a.message} — <span style={{color:'#888'}}>Patient: {a.patientName}</span></div>
                </div>
                <div className="alert-time" style={{fontFamily:'monospace', color:'#666'}}>{new Date().toLocaleTimeString()}</div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* --- OVERLAY: FULL SCREEN MONITOR --- */}
       {activePatient && (
        <div className={monitorClasses.overlay}>
          
          {/* Global Banner */}
          {isCrit && <div className="alert-banner banner-crit">⚠️ CRITICAL CONDITION ⚠️</div>}
          {isWarn && !isCrit && <div className="alert-banner banner-warn">⚠️ PATIENT WARNING ⚠️</div>}

          <div className="mon-header">
            <div style={{fontSize:'20px', fontWeight:'bold'}}>
                LIVE FEED: <span style={{color:'var(--neon-green)'}}>{activePatient.name.toUpperCase()}</span>
                <span style={{fontSize:'12px', color:'#666', marginLeft:'15px'}}>ID: {activePatient.patientId}</span>
            </div>
            <button className="close-btn" onClick={handleCloseMonitor}>CLOSE FEED</button>
          </div>
          
          <div className="mon-grid">
             {/* ROW 1: ECG */}
             <div className="wave-row">
               <div className="canvas-area">
                   <MonitorCanvas type="ecg" color="#00ff41" />
               </div>
               {/* Use pre-calculated class */}
               <div className={`data-sidebar ${monitorClasses.hr}`} style={{color:'var(--neon-green)', borderColor:'var(--neon-green)'}}>
                   <span className="ds-label">Heart Rate</span>
                   <span className="ds-value">{getVal(getGridData(activePatient.patientId), 'ECG')}</span>
                   <span className="ds-unit">BPM</span>
               </div>
             </div>

             {/* ROW 2: SPO2 */}
             <div className="wave-row">
               <div className="canvas-area">
                   <MonitorCanvas type="spo2" color="#00f0ff" />
               </div>
               <div className={`data-sidebar ${monitorClasses.spo2}`} style={{color:'var(--neon-blue)', borderColor:'var(--neon-blue)'}}>
                   <span className="ds-label">SpO2</span>
                   <span className="ds-value">{getVal(getGridData(activePatient.patientId), 'SpO2')}</span>
                   <span className="ds-unit">%</span>
               </div>
             </div>

             {/* ROW 3: TEMP */}
             <div className="wave-row">
               <div className="canvas-area">
                   <MonitorCanvas type="resp" color="#ffee00" />
               </div>
               <div className={`data-sidebar ${monitorClasses.temp}`} style={{color:'var(--neon-yellow)', borderColor:'var(--neon-yellow)'}}>
                   <span className="ds-label">Temp</span>
                   <span className="ds-value">{formatTemp(getVal(getGridData(activePatient.patientId), 'Temp'))}</span>
                   <span className="ds-unit">°C</span>
               </div>
             </div>

             {/* ROW 4: FETAL */}
             {activePatient.isPregnant && (
               <div className="wave-row">
                 <div className="canvas-area">
                     <MonitorCanvas type="ecg" color="#d500f9" />
                 </div>
                 <div className={`data-sidebar ${monitorClasses.fetal}`} style={{color:'var(--neon-purple)', borderColor:'var(--neon-purple)'}}>
                     <span className="ds-label">Fetal HR</span>
                     <span className="ds-value">{getVal(getWaveData(activePatient.patientId), 'FetalHR')}</span>
                     <span className="ds-unit">BPM</span>
                 </div>
               </div>
             )}
          </div>
        </div>
      )}


    </div>
  );
}

// Returns CSS class: "" (Normal), "ds-warning", or "ds-critical"
const checkVitalStatus = (type, val) => {
    const num = parseFloat(val);
    if (isNaN(num)) return ""; // No data, no alert

    switch(type) {
        case 'HR': // Heart Rate (Normal: 60-100)
            if (num > 130 || num < 40) return "ds-critical";
            if (num > 100 || num < 50) return "ds-warning";
            return "";
        case 'SPO2': // Oxygen (Normal: > 95)
            if (num < 90) return "ds-critical";
            if (num < 95) return "ds-warning";
            return "";
        case 'TEMP': // Temp (Normal: 36.5 - 37.5)
            if (num > 39.0) return "ds-critical";
            if (num > 37.5) return "ds-warning";
            return "";
        case 'FETAL': // Fetal HR (Normal: 110-160)
            if (num < 100 || num > 180) return "ds-critical";
            return "";
        default: return "";
    }
};

// Ensure formatTemp is available too if you haven't added it yet
const formatTemp = (val) => {
    const num = Number(val);
    return !isNaN(num) ? num.toFixed(1) : '--';
};



export default App;
