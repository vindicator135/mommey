import { ChatWidget } from './components/ChatWidget';

function App() {
  return (
    <div className="min-h-screen bg-slate-50 relative overflow-hidden">
      {/* Decorative background elements */}
      <div className="absolute top-0 right-0 -translate-y-1/2 translate-x-1/2 w-[500px] h-[500px] bg-blue-100/50 rounded-full blur-3xl pointer-events-none"></div>
      <div className="absolute bottom-0 left-0 translate-y-1/2 -translate-x-1/2 w-[500px] h-[500px] bg-indigo-100/50 rounded-full blur-3xl pointer-events-none"></div>

      <main className="relative z-10 max-w-5xl mx-auto px-6 pt-24 pb-12">
        <header className="text-center mb-16">
          <div className="inline-block px-4 py-1.5 mb-6 text-sm font-semibold tracking-wide text-blue-600 uppercase bg-blue-50 rounded-full">
            Development Harness
          </div>
          <h1 className="text-5xl font-extrabold text-slate-900 mb-6 tracking-tight">
            Mommey <span className="text-blue-600">Widget Test</span>
          </h1>
          <p className="text-xl text-slate-600 max-w-2xl mx-auto leading-relaxed">
            This page simulates a live website where the Mommey widget is embedded. 
            Use the chat bubble below to start interacting with the AI.
          </p>
        </header>

        <section className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-12">
          <div className="bg-white p-8 rounded-3xl shadow-sm border border-slate-100">
            <h2 className="text-2xl font-bold text-slate-800 mb-4">Integration Guide</h2>
            <p className="text-slate-600 mb-6">
              To embed this widget on any site, include the CDN script and use the mount function:
            </p>
            <div className="bg-slate-900 rounded-xl p-4 overflow-x-auto">
              <code className="text-sm text-blue-300">
                &lt;script src="cdn.mommey.com/widget.js"&gt;&lt;/script&gt;<br/>
                &lt;script&gt;<br/>
                &nbsp;&nbsp;Mommey.init(&#123; target: '#chat' &#125;);<br/>
                &lt;/script&gt;
              </code>
            </div>
          </div>
          
          <div className="bg-white p-8 rounded-3xl shadow-sm border border-slate-100 flex flex-col justify-center items-center text-center">
            <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-2xl flex items-center justify-center mb-4">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
            <h3 className="text-xl font-bold text-slate-800 mb-2">Real-time Updates</h3>
            <p className="text-slate-600">
              The widget connects to your C# BFF to orchestrate Notion and Google tasks.
            </p>
          </div>
        </section>

        <footer className="text-center text-slate-400 text-sm">
          &copy; 2026 Moms Mate Project • Built with React, TypeScript & .NET 9
        </footer>
      </main>

      {/* The Widget */}
      <ChatWidget />
    </div>
  );
}

export default App;
