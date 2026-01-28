import React, { memo } from 'react';

// Helper for safe access (handles uppercase/lowercase keys)
const getVal = (obj, key) => {
    if (!obj) return '--';
    return obj[key] ?? obj[key.toLowerCase()] ?? obj[key.toUpperCase()] ?? '--';
};

const PatientCard = memo(({ patient, vitals, onMonitor }) => {
    const status = getVal(vitals, 'Status');
    const isCrit = status === "Critical";
    const isWarn = status === "Warning";
    
    // Dynamic Styles for Status
    let borderColor = '#333';
    let statusColor = '#00ff41'; // Green
    
    if (isCrit) { borderColor = 'red'; statusColor = 'red'; }
    else if (isWarn) { borderColor = 'orange'; statusColor = 'orange'; }

    return (
        <div className="patient-card" style={{ borderColor: isCrit ? 'red' : (isWarn ? 'orange' : '#333') }}>
            <div className="card-header">
                <div style={{display:'flex', alignItems:'center'}}>
                    <span className={`status-dot ${isCrit ? 'status-crit' : 'status-ok'}`}
                          style={{ background: statusColor }}></span>
                    <span className="patient-name">{patient.name}</span>
                </div>
                <span className="status-badge" style={{ background: statusColor, color: isCrit ? 'white' : 'black' }}>
                    {status !== '--' ? status : 'NORMAL'}
                </span>
            </div>

            <div style={{ fontSize: '13px', color: '#888' }}>
                <div>ID: #{patient.patientId}</div>
                <div>Dr. {patient.doctorName}</div>
            </div>

            {/* LIVE METRICS BOX - Now with 3 items */}
            <div className="live-metrics">
                <div className="metric-box">
                    <span className="m-label">HR (BPM)</span>
                    <span className="m-val" style={{color:'var(--neon-green)'}}>{getVal(vitals, 'ECG')}</span>
                </div>
                <div className="metric-box">
                    <span className="m-label">SpO2 (%)</span>
                    <span className="m-val" style={{color:'var(--neon-blue)'}}>{getVal(vitals, 'SpO2')}</span>
                </div>
                {/* ADDED: Temperature Section */}
                <div className="metric-box">
                    <span className="m-label">Temp (°C)</span>
                    <span className="m-val" style={{color:'var(--neon-yellow)'}}>{getVal(vitals, 'Temp')}</span>
                </div>
            </div>

            <button className="btn-monitor" onClick={() => onMonitor(patient)}>
                OPEN MONITORING
            </button>
        </div>
    );
});

export default PatientCard;
