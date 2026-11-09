import SwiftUI

@main
struct RealEstateApp: App {
    var body: some Scene {
        WindowGroup {
            SiteView(url: Site.url)
                .ignoresSafeArea(edges: .bottom)
        }
    }
}
