
import React, { useEffect, useRef } from 'react';


const MonitorCanvas = ({ type, color }) => {
    const canvasRef = useRef(null);
    const containerRef = useRef(null);
    const stateRef = useRef({ x: 0, lastY: 0, phase: 0 }); // Mutable state for animation


    useEffect(() => {
        const cvs = canvasRef.current;
        const ctx = cvs.getContext('2d');
        let animationId;
        const speed = 2; // Sweep speed


        const resize = () => {
            const parent = containerRef.current;
            if(parent) {
                cvs.width = parent.clientWidth;
                cvs.height = parent.clientHeight;
                stateRef.current.lastY = cvs.height / 2;
            }
        };
        window.addEventListener('resize', resize);
        resize();


        // --- WAVE MATH ---
        const getECG = (p) => {
            let y = 0;
            if(p > 0.10 && p < 0.16) y -= 0.15 * Math.sin((p-0.10)/0.06 * Math.PI);
            if(p > 0.18 && p < 0.20) y += 0.15 * Math.sin((p-0.18)/0.02 * Math.PI);
            if(p > 0.20 && p < 0.24) y -= 1.0 * Math.sin((p-0.20)/0.04 * Math.PI); // R Spike
            if(p > 0.24 && p < 0.27) y += 0.3 * Math.sin((p-0.24)/0.03 * Math.PI);
            if(p > 0.35 && p < 0.50) y -= 0.2 * Math.sin((p-0.35)/0.15 * Math.PI);
            return y;
        };


        const getPleth = (p) => {
            p = (p + 0.8) % 1.0;
            let y = Math.pow(Math.sin(p * Math.PI), 8);
            if (p > 0.3) y += 0.3 * Math.sin((p - 0.3) * 3 * Math.PI) * Math.exp(-(p - 0.3) * 10);
            return y;
        };


        const getResp = (p) => Math.sin(p * 2 * Math.PI) * 0.5;


        // --- ANIMATION LOOP ---
        const draw = () => {
            const { width, height } = cvs;
            const midY = height / 2;
            const s = stateRef.current;


            // 1. Erase Bar
            ctx.clearRect(s.x, 0, 10, height);


            // 2. Calculate Y
            s.phase += 0.01; // Frequency
            if(s.phase > 1) s.phase = 0;


            let val = 0;
            if (type === 'ecg') val = getECG(s.phase);
            else if (type === 'spo2') val = getPleth(s.phase);
            else if (type === 'resp') val = getResp(s.phase);


            let nextY = midY + (val * (height * 0.3));
            if(type === 'spo2') nextY = (height * 0.9) - (val * (height * 0.7)); // Pleth sits at bottom


            // 3. Draw Line
            ctx.beginPath();
            ctx.strokeStyle = color;
            ctx.lineWidth = 2;
            ctx.lineCap = 'round';
           
            if (s.x > 0) ctx.moveTo(s.x - speed, s.lastY);
            else ctx.moveTo(s.x, nextY);
           
            ctx.lineTo(s.x, nextY);
            ctx.stroke();


            // 4. Advance
            s.x += speed;
            s.lastY = nextY;
            if (s.x > width) { s.x = 0; ctx.moveTo(0, nextY); }


            animationId = requestAnimationFrame(draw);
        };


        draw();
        return () => {
            window.removeEventListener('resize', resize);
            cancelAnimationFrame(animationId);
        };
    }, [type, color]);


    return (
        <div ref={containerRef} style={{ width: '100%', height: '100%' }}>
            <canvas ref={canvasRef} style={{display:'block'}} />
        </div>
    );
};


export default MonitorCanvas;





