📋 Diagnova — Detailed Feature Summary

1. 💬 AI Symptom Checker
Diagnova allows users to describe their symptoms in natural language through a chat interface. The app sends these symptoms to OpenAI GPT-4 API with a carefully designed medical system prompt. The AI analyzes the input and returns a list of possible medical conditions, severity level (mild, moderate, or severe), and whether the user should rest at home or visit a doctor immediately. The response is displayed in a clean chat UI with clear formatting.

2. 🧠 Conversation Memory
Every message sent by the user and every reply from the AI is instantly saved to a local database. When the user returns next time, the full chat history is loaded from the database and sent to GPT as context. This means the bot remembers past symptoms, previous diagnoses, and ongoing health complaints — giving more accurate and personalized responses over time just like a real doctor who knows your history.

3. 💊 Medicine Information
Users can search for any medicine by name inside the app. Diagnova connects to the OpenFDA API and retrieves detailed information including dosage instructions, usage guidelines, side effects, warnings, and the difference between generic and brand name versions. This helps users understand their prescriptions better without needing to search multiple websites.

4. 🩺 Specialist Suggestion
After analyzing the symptoms, Diagnova automatically recommends which type of medical specialist the user should visit. The AI maps symptoms to relevant specialties — for example chest pain leads to a Cardiologist, skin issues lead to a Dermatologist, and mental health concerns lead to a Psychiatrist. A brief explanation is also provided so the user understands why that specialist is recommended.

5. 🏨 Nearby Hospital & Doctor Finder
This is one of the most powerful features of Diagnova. Based on the diagnosed symptoms and detected live location of the user, the app uses Google Maps API and Google Places API to find the nearest hospitals and relevant doctors. Results are displayed on an interactive map with hospital name, distance, ratings, and a one-click navigation button that opens Google Maps for directions. Users can also filter results by specialty.

6. ⚠️ Emergency Detector
Before sending any message to the AI, Diagnova scans the input locally for dangerous emergency keywords such as chest pain, can't breathe, heavy bleeding, or stroke. If detected, a full screen red alert is immediately shown without any API delay. The alert displays Pakistan emergency helpline numbers like 1122 and Rescue, and also triggers the nearby hospital finder to show the closest emergency room instantly.

7. 🌡️ Vitals Tracker
Users can manually log their daily health vitals including blood pressure, blood sugar level, and body temperature. All readings are stored in the database and displayed as visual line and bar graphs showing trends over days and weeks. The app highlights abnormal readings with color coded alerts and allows users to generate a vitals report to share with their doctor during a visit.

8. 🔊 Voice Input
Users can speak their symptoms instead of typing them using the built-in voice input feature powered by Azure Speech SDK. The spoken words are converted to text and automatically filled into the chat input box. This feature is especially useful for elderly patients or users who are not comfortable typing and also supports both English and Urdu languages.

9. 📋 Patient History
Every chat session is stored in the database with full details including date, time, symptoms described, and AI response. Users can browse their complete medical chat history in a timeline view, search past sessions by date or keyword, and export the entire history as a professional PDF report. This report can be shared with a real doctor for better consultation.

10. 🔐 User Authentication
Diagnova includes a secure login and registration system for patients. Each user has their own private profile with personal health data, chat history, and vitals completely separate from other users. All data is password protected and stored locally in a secure database.

11. 📊 Health Dashboard
The main dashboard gives users a complete overview of their health at a glance. It shows a summary of the most recent diagnosis, latest vitals with mini graphs, quick access buttons to all features, and any pending health alerts. The dashboard is designed to be clean, modern, and easy to navigate for all age groups.

🔗 APIs & Technologies Used
TechnologyPurposeOpenAI GPT-4 APISymptom analysis and conversationOpenFDA APIMedicine information lookupGoogle Maps APIInteractive hospital mapGoogle Places APINearby doctors and hospitalsGoogle Geolocation APIDetect live user locationAzure Speech SDKVoice to text inputSQLite DatabaseSave chats, vitals, user dataLiveCharts2Vitals graphs and chartsC# .NET / WPFCore application framework
